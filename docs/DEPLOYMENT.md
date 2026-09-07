# SplitIt — Production Deployment

Deploys happen automatically on push to `main` via GitHub Actions. This guide covers the one-time VPS setup and manual deploy commands.

## VPS Setup (one-time)
```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Clone the repo
sudo mkdir -p /opt/splitit
sudo chown $USER:docker /opt/splitit
git clone https://github.com/santidev21/SplitIt.git /opt/splitit

# Configure environment
cd /opt/splitit
cp .env.example .env
# Edit .env with production secrets

# Create Docker network
docker network create splitit-net

# Start services
docker compose up -d
```

## Deploy Updates
Deploys happen automatically on push to `main` via GitHub Actions. Manual deploy:
```bash
cd /opt/splitit
./scripts/deploy.sh deploy
```

## Other Deploy Commands
```bash
./scripts/deploy.sh status    # Show container status
./scripts/deploy.sh logs      # Show recent logs
./scripts/deploy.sh rollback  # Rollback to last backup
./scripts/deploy.sh verify    # Verify all services healthy
```
