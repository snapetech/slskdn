// <copyright file="FTPClientFactory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// <copyright file="FTPClientFactory.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>
using Microsoft.Extensions.Options;

namespace slskd.Integrations.FTP
{
    using System;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using FluentFTP;
    using static slskd.Options.IntegrationOptions;

    /// <summary>
    ///     FTP client factory.
    /// </summary>
    public class FTPClientFactory : IFTPClientFactory
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FTPClientFactory"/> class.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor used to derive application options.</param>
        public FTPClientFactory(IOptionsMonitor<Options> optionsMonitor)
        {
            OptionsMonitor = optionsMonitor;
        }

        private FtpOptions FtpOptions => OptionsMonitor.CurrentValue.Integration.Ftp;
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }

        /// <summary>
        ///     Creates an instance of <see cref="FtpClient"/>.
        /// </summary>
        /// <returns>The created instance.</returns>
        public AsyncFtpClient CreateFtpClient()
        {
            var config = new FtpConfig
            {
                EncryptionMode = FtpOptions.EncryptionMode.ToEnum<FtpEncryptionMode>(),
            };

            var client = new AsyncFtpClient(
                FtpOptions.Address,
                FtpOptions.Username,
                FtpOptions.Password,
                FtpOptions.Port,
                config);

            if (FtpOptions.IgnoreCertificateErrors)
            {
                if (client.Config.ValidateAnyCertificate is false)
                {
                    client.Config.ValidateAnyCertificate = true;
                }

                client.ValidateCertificate += (_, e) =>
                {
                    if (e.PolicyErrors == SslPolicyErrors.None)
                    {
                        e.Accept = true;
                        return;
                    }

                    if (IsAllowedInsecureFtpCertificate(e.Certificate, e.Chain, e.PolicyErrors))
                    {
                        e.Accept = true;
                    }
                };
            }

            return client;
        }

        private static bool IsAllowedInsecureFtpCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null || sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                return false;
            }

            var certificate2 = certificate as X509Certificate2;
            if (certificate2 == null || !string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.Ordinal))
            {
                return false;
            }

            if (chain == null || chain.ChainStatus.Length == 0)
            {
                return false;
            }

            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                    status.Status != X509ChainStatusFlags.PartialChain)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
