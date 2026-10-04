#!/bin/bash
set -euxo pipefail
# Amazon Linux 2023 / Ubuntu friendly bootstrap for UNO relay
if command -v dnf >/dev/null 2>&1; then
  dnf install -y nodejs npm git
elif command -v apt-get >/dev/null 2>&1; then
  apt-get update -y
  apt-get install -y nodejs npm git
fi

mkdir -p /opt/uno3d-relay
cat > /opt/uno3d-relay/package.json <<'EOF'
{
  "name": "uno3d-relay",
  "version": "1.0.0",
  "private": true,
  "main": "relay.js",
  "scripts": { "start": "node relay.js" }
}
EOF

# relay.js is uploaded separately by deploy script; placeholder until then
if [ ! -f /opt/uno3d-relay/relay.js ]; then
  echo "console.log('waiting for relay.js'); setInterval(()=>{}, 60000);" > /opt/uno3d-relay/relay.js
fi

cat > /etc/systemd/system/uno3d-relay.service <<'EOF'
[Unit]
Description=UNO 3D Relay
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/uno3d-relay
Environment=TCP_PORT=17777
Environment=HEALTH_PORT=17779
ExecStart=/usr/bin/node /opt/uno3d-relay/relay.js
Restart=always
RestartSec=3
User=root

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable uno3d-relay
systemctl restart uno3d-relay || true

# Open ports via firewalld if present
if command -v firewall-cmd >/dev/null 2>&1; then
  firewall-cmd --permanent --add-port=17777/tcp || true
  firewall-cmd --permanent --add-port=17779/tcp || true
  firewall-cmd --reload || true
fi
