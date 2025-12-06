# AUR Packaging for slskdn

## Packages

- **slskdn** - Build from source (requires dotnet-sdk, nodejs)
- **slskdn-bin** - Pre-built binary release (recommended)

## Manual AUR Setup (One-time)

### 1. Create AUR Account
Go to https://aur.archlinux.org/register and create an account.

### 2. Add SSH Key
```bash
# Generate SSH key for AUR
ssh-keygen -t ed25519 -f ~/.ssh/aur -C "your-email@example.com"

# Add to AUR account at https://aur.archlinux.org/account/your-username/
cat ~/.ssh/aur.pub
```

### 3. Configure SSH
```bash
echo "Host aur.archlinux.org
  IdentityFile ~/.ssh/aur
  User aur" >> ~/.ssh/config
```

### 4. Create AUR Package Repository
```bash
# Clone empty AUR repo (first time)
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git

# Or create new package
ssh aur@aur.archlinux.org setup-repo slskdn-bin
git clone ssh://aur@aur.archlinux.org/slskdn-bin.git
```

### 5. Push Package
```bash
cd slskdn-bin
cp /path/to/slskdn/packaging/aur/PKGBUILD-bin PKGBUILD
cp /path/to/slskdn/packaging/aur/slskdn.* .
makepkg --printsrcinfo > .SRCINFO
git add PKGBUILD .SRCINFO slskdn.*
git commit -m "Initial upload: slskdn-bin 0.24.1.slskdn.3"
git push
```

## GitHub Actions Automation

To enable automatic AUR updates on release:

1. Add your AUR SSH private key as a GitHub secret named `AUR_SSH_KEY`
2. The `release-linux.yml` workflow will automatically update AUR on new releases

## Testing Locally

```bash
cd packaging/aur
makepkg -si  # Build and install
```

## Installing from AUR

```bash
# Using yay
yay -S slskdn-bin

# Using paru  
paru -S slskdn-bin

# Manual
git clone https://aur.archlinux.org/slskdn-bin.git
cd slskdn-bin
makepkg -si
```

## Post-Install

```bash
# Edit config
sudo nano /etc/slskdn/slskdn.yml

# Start service
sudo systemctl enable --now slskdn

# Check status
sudo systemctl status slskdn
journalctl -u slskdn -f
```
