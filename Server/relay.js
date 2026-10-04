'use strict';

/**
 * UNO 3D TCP room relay.
 * Host and clients connect outbound to this server (no port-forward on players).
 * Line-delimited JSON envelopes are forwarded within a room.
 */

const net = require('net');
const http = require('http');

const TCP_PORT = Number(process.env.TCP_PORT || 17777);
const HEALTH_PORT = Number(process.env.HEALTH_PORT || 17779);
const MAX_ROOMS = Number(process.env.MAX_ROOMS || 200);
const MAX_CLIENTS_PER_ROOM = 4;

/** @type {Map<string, Room>} */
const rooms = new Map();

class Room {
  /**
   * @param {string} code
   * @param {net.Socket} hostSocket
   */
  constructor(code, hostSocket) {
    this.code = code;
    this.host = hostSocket;
    /** @type {Map<string, net.Socket>} */
    this.clients = new Map();
    this.createdAt = Date.now();
  }
}

function genCode() {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let out = '';
  for (let i = 0; i < 6; i++) {
    out += alphabet[Math.floor(Math.random() * alphabet.length)];
  }
  return out;
}

function send(socket, obj) {
  if (!socket || socket.destroyed) return;
  try {
    socket.write(JSON.stringify(obj) + '\n');
  } catch (_) {
    /* ignore */
  }
}

function parseEnvelope(line) {
  try {
    return JSON.parse(line);
  } catch {
    return null;
  }
}

function destroySocket(socket) {
  try {
    socket.destroy();
  } catch (_) {
    /* ignore */
  }
}

function leaveRoom(socket) {
  const role = socket._unoRole;
  const code = socket._unoRoom;
  if (!code) return;
  const room = rooms.get(code);
  if (!room) return;

  if (role === 'host') {
    for (const client of room.clients.values()) {
      send(client, { Type: 3, PayloadJson: JSON.stringify({ reason: 'host_left' }) });
      destroySocket(client);
    }
    rooms.delete(code);
    console.log(`[relay] room ${code} closed (host left)`);
    return;
  }

  if (role === 'client' && socket._unoClientId) {
    room.clients.delete(socket._unoClientId);
    // Notify host so Unity can free the seat.
    send(room.host, {
      Type: 10,
      PayloadJson: JSON.stringify({ ClientId: socket._unoClientId })
    });
    console.log(`[relay] client ${socket._unoClientId} left room ${code}`);
  }
}

function attachLineReader(socket, onLine) {
  let buffer = '';
  socket.setEncoding('utf8');
  socket.on('data', (chunk) => {
    buffer += chunk;
    let idx;
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).trim();
      buffer = buffer.slice(idx + 1);
      if (line) onLine(line);
    }
  });
}

const tcpServer = net.createServer((socket) => {
  socket._unoRole = 'unknown';
  socket.setTimeout(120000);
  socket.on('timeout', () => destroySocket(socket));
  socket.on('error', () => leaveRoom(socket));
  socket.on('close', () => leaveRoom(socket));

  attachLineReader(socket, (line) => {
    const env = parseEnvelope(line);
    if (!env || typeof env.Type !== 'number') return;

    // Bootstrap: host/client hello before room association.
    if (socket._unoRole === 'unknown') {
      let payload = {};
      try {
        payload = JSON.parse(env.PayloadJson || '{}');
      } catch {
        payload = {};
      }

      if (env.Type === 100) {
        // HostCreateRoom
        if (rooms.size >= MAX_ROOMS) {
          send(socket, { Type: 3, PayloadJson: JSON.stringify({ reason: 'server_full' }) });
          destroySocket(socket);
          return;
        }
        let code = (payload.RoomCode || '').toUpperCase();
        if (!code || rooms.has(code)) code = genCode();
        while (rooms.has(code)) code = genCode();
        socket._unoRole = 'host';
        socket._unoRoom = code;
        rooms.set(code, new Room(code, socket));
        send(socket, {
          Type: 101,
          PayloadJson: JSON.stringify({ RoomCode: code, TcpPort: TCP_PORT })
        });
        console.log(`[relay] host created room ${code}`);
        return;
      }

      if (env.Type === 102) {
        // ClientJoinRoom
        const code = String(payload.RoomCode || '').toUpperCase();
        const clientId = payload.ClientId || genCode() + Date.now().toString(36);
        const room = rooms.get(code);
        if (!room) {
          send(socket, { Type: 3, PayloadJson: JSON.stringify({ reason: 'room_not_found' }) });
          destroySocket(socket);
          return;
        }
        if (room.clients.size >= MAX_CLIENTS_PER_ROOM - 1) {
          send(socket, { Type: 3, PayloadJson: JSON.stringify({ reason: 'room_full' }) });
          destroySocket(socket);
          return;
        }
        socket._unoRole = 'client';
        socket._unoRoom = code;
        socket._unoClientId = clientId;
        room.clients.set(clientId, socket);
        send(socket, {
          Type: 103,
          PayloadJson: JSON.stringify({ RoomCode: code, ClientId: clientId })
        });
        // Forward original join request to host as Type 1 JoinRequest.
        send(room.host, {
          Type: 1,
          PayloadJson: JSON.stringify({
            PlayerName: payload.PlayerName || 'Guest',
            RoomCode: code,
            ClientId: clientId
          })
        });
        console.log(`[relay] client ${clientId} joined room ${code}`);
        return;
      }

      send(socket, { Type: 3, PayloadJson: JSON.stringify({ reason: 'expected_hello' }) });
      return;
    }

    const room = rooms.get(socket._unoRoom);
    if (!room) return;

    if (socket._unoRole === 'host') {
      // Host → all clients (or targeted if Payload includes ClientId for snapshots).
      let targetedId = null;
      try {
        const p = JSON.parse(env.PayloadJson || '{}');
        if (p && p._RelayTargetClientId) targetedId = p._RelayTargetClientId;
      } catch {
        /* ignore */
      }

      if (targetedId) {
        const target = room.clients.get(targetedId);
        if (target) send(target, env);
        return;
      }

      for (const client of room.clients.values()) {
        send(client, env);
      }
      return;
    }

    if (socket._unoRole === 'client') {
      // Client → host
      send(room.host, env);
    }
  });
});

tcpServer.listen(TCP_PORT, '0.0.0.0', () => {
  console.log(`[relay] TCP listening on 0.0.0.0:${TCP_PORT}`);
});

const health = http.createServer((req, res) => {
  if (req.url === '/health' || req.url === '/') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(
      JSON.stringify({
        service: 'uno3d-relay',
        ok: true,
        rooms: rooms.size,
        tcpPort: TCP_PORT
      })
    );
    return;
  }
  res.writeHead(404);
  res.end('not found');
});

health.listen(HEALTH_PORT, '0.0.0.0', () => {
  console.log(`[relay] health on 0.0.0.0:${HEALTH_PORT}`);
});
