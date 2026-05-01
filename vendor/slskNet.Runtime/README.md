# slskNet.Runtime

slskNet.Runtime is a slskdN-maintained .NET Standard runtime transport library for the Soulseek network.

This is a modified version of Soulseek.NET. It is not maintained by, endorsed by, or affiliated with the Soulseek.NET project or its author(s).

## Runtime Changes

- Package and repository branding changed to `slskNet.Runtime`.
- Optional Soulseek type-1 peer-message obfuscation metadata is supported in `SetListenPort`, peer-address responses, and `ConnectToPeer` responses.
- A dedicated type-1 obfuscated peer-message listener can be configured.
- Outbound peer-message dials can prefer compatible obfuscated endpoints while keeping regular and indirect fallback paths.

Type-1 obfuscation is not encryption. It is a compatibility/privacy posture for peer-message streams only; file-transfer and distributed-network streams remain regular transport.

## License

This software is licensed under the GNU General Public License v3.0 with Additional Terms pursuant to Section 7 of the GPLv3. The complete license text is in [LICENSE](LICENSE), and the required notices are in [NOTICE](NOTICE).

Original Soulseek.NET copyright and license notices are preserved in source files. Modified source files include slskdN modification notices.

## Reserved Minor Version Ranges

Applications using this library are required, as a condition of the license, to use a unique minor version number when logging in to the Soulseek network.

- `760-7699999`: slskd
- `7700000+`: reserved by slskdN/slskNet.Runtime deployments unless coordinated otherwise

## References

- [Nicotine+ protocol documentation](https://nicotine-plus.org/doc/SLSKPROTOCOL.html)
- [SoulseekProtocol - Museek+](https://www.museek-plus.org/wiki/SoulseekProtocol)
- Original project: Soulseek.NET by JP Dillingham
