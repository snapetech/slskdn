Name:           slskdn
Version:        0.24.1.slskdn.7
Release:        1%{?dist}
Summary:        🔋 The batteries included fork of slskd with 24+ new features

License:        AGPL-3.0-or-later
URL:            https://github.com/snapetech/slskdn
Source0:        https://github.com/snapetech/slskdn/releases/download/%{version}/slskdn-%{version}-linux-x64.zip
Source1:        slskd.service
Source2:        slskd.yml
Source3:        slskd.conf

BuildArch:      x86_64
BuildRequires:  systemd-rpm-macros
BuildRequires:  unzip
# Required for user creation in %pre
Requires(pre):  shadow-utils
Requires:       systemd
Provides:       slskd

# Disable debuginfo - this is a pre-built .NET binary
%global debug_package %{nil}
%define __strip /bin/true
Conflicts:      slskd
Obsoletes:      slskd < %{version}

%description
The batteries included fork of slskd with 24+ new features: decentralized pods,
content validation, swarm downloads, DHT mesh networking, auto-replace, wishlist,
security hardening.

Stable Features:
- Auto-replace stuck downloads - Automatically finds and switches to working sources
- Wishlist/background search - Never miss rare content with automated searches
- Smart source ranking - Intelligent scoring based on speed, queue, and history
- Tabbed browsing - Browse multiple users simultaneously with persistent state
- User notes & ratings - Color-coded ratings and persistent notes for users
- Push notifications - Ntfy and Pushover support for messages and mentions
- Multi-select folder downloads - Download multiple folders with checkbox selection
- Advanced search filters - Visual filter editor with bitrate, duration, file size
- PWA/mobile support - Install as standalone app on iOS/Android
- Delete files on disk - Remove downloads and delete files in one action
- Block users from results - Hide specific users from search results
- Save search filters - Persistent filter preferences across sessions
- Chat room improvements - Right-click context menu for browse/chat/notes
- Multiple download destinations - Configure multiple folders with routing
- Download history badges - Visual indicators for successful download history
- Clear all searches - One-click cleanup of search history
- Configurable search page size - 25 to 500 results per page
- Multi-source swarm downloads - Download from multiple peers simultaneously
- DHT mesh networking - Decentralized peer discovery and mesh overlay
- CSRF protection - Full CSRF token validation for web UI
- Security hardening - NetworkGuard, fingerprint detection, security profiles

Advanced Features:
- Decentralized pod communities - Private micro-communities over mesh overlay with DHT discovery
- Content validation - Byzantine consensus & proof-of-storage with cryptographic commitments
- MusicBrainz integration - Metadata enrichment, library health scanning, AcoustID fingerprinting
- Service fabric - Generic mesh service layer for decentralized applications

Modern web UI for the Soulseek network. Drop-in replacement for slskd.

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
echo "🔋 slskdn has been installed!"
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
* Sat Dec 06 2025 snapetech <slskdn@proton.me> - 0.24.1.slskdn.7-1
- Fix race condition in SourceRankingService
- Update branding

* Fri Dec 05 2025 snapetech <slskdn@proton.me> - 0.24.1.slskdn.6-1
- Initial slskdn release
