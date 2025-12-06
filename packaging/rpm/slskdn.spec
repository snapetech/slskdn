Name:           slskdn
Version:        0.24.1.slskdn.7
Release:        1%{?dist}
Summary:        🔋 The batteries-included Soulseek web client

License:        AGPL-3.0-or-later
URL:            https://github.com/snapetech/slskdn
Source0:        https://github.com/snapetech/slskdn/releases/download/%{version}/slskdn-%{version}-linux-x64.zip
Source1:        slskd.service
Source2:        slskd.yml
Source3:        slskd.conf

BuildArch:      x86_64
BuildRequires:  systemd-rpm-macros
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
A feature-rich fork of slskd with wishlist, smart ranking, tabbed
browsing, notifications, and more. Modern web UI for the Soulseek
file sharing network.

Features include:
- Wishlist with auto-download
- Smart source ranking based on speed, queue, and history
- Tabbed user browsing with persistent sessions
- Multi-select folder downloads with checkboxes
- Configurable search page size
- Max file size filter
- Delete files on disk when removing downloads
- Push notifications (Pushbullet, Ntfy, Pushover)
- PWA support for mobile

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
