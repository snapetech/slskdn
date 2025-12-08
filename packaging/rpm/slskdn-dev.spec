Name:           slskdn-dev
Version:        0.24.1.dev.202412080000
Release:        1%{?dist}
Summary:        🔋 DEV: The batteries included, ***EXPERIMENTAL*** fork of slskd. Feature-rich, including multi-source downloads, DHT mesh sync, swarm mode & more

License:        AGPL-3.0-or-later
URL:            https://github.com/snapetech/slskdn/tree/experimental/multi-source-swarm
Source0:        slskdn-dev-linux-x64.zip
Source1:        slskd.service
Source2:        slskd.yml
Source3:        slskd.conf

BuildArch:      x86_64
BuildRequires:  systemd-rpm-macros
BuildRequires:  unzip
Requires(pre):  shadow-utils
Requires:       systemd
Provides:       slskd
Provides:       slskdn

# Disable debuginfo - this is a pre-built .NET binary
%global debug_package %{nil}
%define __strip /bin/true
Conflicts:      slskd
Conflicts:      slskdn
Obsoletes:      slskd < %{version}

%description
🔋 DEV: The batteries included, ***EXPERIMENTAL*** fork of slskd. Feature-rich, including multi-source downloads, DHT mesh sync, swarm mode & more

%prep
%setup -q -c

%install
# Install application
install -dm755 %{buildroot}%{_libdir}/slskd
cp -r * %{buildroot}%{_libdir}/slskd/
chmod +x %{buildroot}%{_libdir}/slskd/slskd

# Create symlink
install -dm755 %{buildroot}%{_bindir}
ln -sf %{_libdir}/slskd/slskd %{buildroot}%{_bindir}/slskd

# Install systemd service
install -Dm644 %{SOURCE1} %{buildroot}%{_unitdir}/slskd.service

# Install config
install -dm755 %{buildroot}%{_sysconfdir}/slskd
install -Dm644 %{SOURCE2} %{buildroot}%{_sysconfdir}/slskd/slskd.yml

# Install sysusers
install -Dm644 %{SOURCE3} %{buildroot}%{_sysusersdir}/slskd.conf

# Create data directories
install -dm755 %{buildroot}%{_sharedstatedir}/slskd
install -dm755 %{buildroot}%{_sharedstatedir}/slskd/downloads
install -dm755 %{buildroot}%{_sharedstatedir}/slskd/incomplete

%pre
getent passwd slskd >/dev/null || useradd -r -s /sbin/nologin -d %{_sharedstatedir}/slskd -c "slskd service account" slskd

%post
%systemd_post slskd.service
echo ""
echo "🧪 slskdn-dev has been installed!"
echo ""
echo "⚠️  This is a DEVELOPMENT build with experimental features:"
echo "   - Multi-source swarm downloads"
echo "   - DHT mesh network"
echo "   - Content hash verification"
echo ""
echo "1. Edit the configuration:"
echo "   sudo nano /etc/slskd/slskd.yml"
echo ""
echo "2. Start the service:"
echo "   sudo systemctl enable --now slskd"
echo ""
echo "3. Access the web UI at:"
echo "   http://localhost:5030"
echo ""
echo "Report issues: https://github.com/snapetech/slskdn/issues"
echo ""

%preun
%systemd_preun slskd.service

%postun
%systemd_postun_with_restart slskd.service

%files
%license %{_libdir}/slskd/LICENSE
%{_libdir}/slskd/
%{_bindir}/slskd
%{_unitdir}/slskd.service
%config(noreplace) %{_sysconfdir}/slskd/slskd.yml
%{_sysusersdir}/slskd.conf
%dir %attr(755,slskd,slskd) %{_sharedstatedir}/slskd
%dir %attr(755,slskd,slskd) %{_sharedstatedir}/slskd/downloads
%dir %attr(755,slskd,slskd) %{_sharedstatedir}/slskd/incomplete

%changelog
* Sun Dec 08 2024 snapetech <slskdn@proton.me> - 0.24.1.dev.202412080000-1
- Initial dev release from experimental/multi-source-swarm
- Multi-source swarm downloads with content verification
- DHT mesh network for peer discovery
- BitTorrent DHT rendezvous layer
- Overlay protocol with TLS security
- Hash database with mesh sync
- Phase 6.5 enhancements (NAT detection, peer verification, etc.)

