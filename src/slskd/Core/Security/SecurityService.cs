// <copyright file="SecurityService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

﻿// <copyright file="SecurityService.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Net;
    using System.Security.Claims;
    using Microsoft.IdentityModel.Tokens;
    using NetTools;
    using slskd.Authentication;

    public interface ISecurityService
    {
        JwtSecurityToken GenerateJwt(string username, Role role, int? ttl = null);
        (string Name, Role Role) AuthenticateWithApiKey(string key, IPAddress callerIpAddress);
        void RevokeToken(string jti);
        bool IsTokenRevoked(string jti);
    }

    public class SecurityService : ISecurityService
    {
        // In-memory deny-list: jti → expiry. Cleared on restart (acceptable since all JWTs are also invalidated on restart
        // when using an ephemeral signing key). Periodically swept to avoid unbounded growth.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _revokedJtis = new();
        private static DateTimeOffset _lastSweep = DateTimeOffset.UtcNow;
        public SecurityService(
            SymmetricSecurityKey jwtSigningKey,
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor)
        {
            JwtSigningKey = jwtSigningKey;
            OptionsAtStartup = optionsAtStartup;
            OptionsMonitor = optionsMonitor;
        }

        private SymmetricSecurityKey JwtSigningKey { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        public (string Name, Role Role) AuthenticateWithApiKey(string key, IPAddress callerIpAddress)
        {
            var inputKeyBytes = System.Text.Encoding.UTF8.GetBytes(key ?? string.Empty);
            var record = OptionsMonitor.CurrentValue.Web.Authentication.ApiKeys
                .FirstOrDefault(k =>
                {
                    var storedKeyBytes = System.Text.Encoding.UTF8.GetBytes(k.Value.Key ?? string.Empty);
                    var padLen = Math.Max(inputKeyBytes.Length, storedKeyBytes.Length);
                    var aPad = new byte[padLen];
                    var bPad = new byte[padLen];
                    inputKeyBytes.CopyTo(aPad, 0);
                    storedKeyBytes.CopyTo(bPad, 0);
                    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aPad, bPad);
                });

            if (record.Key == null)
            {
                throw new NotFoundException($"The provided API key does not match an existing key");
            }

            if (!record.Value.Cidr.Split(',')
                .Select(cidr => IPAddressRange.Parse(cidr))
                .Any(range => range.Contains(callerIpAddress)))
            {
                throw new OutOfRangeException("The remote IP address is not within the range specified for the key");
            }

            return (record.Key, record.Value.Role.ToEnum<Role>());
        }

        public void RevokeToken(string jti)
        {
            if (string.IsNullOrWhiteSpace(jti)) return;
            // We don't know the original expiry here, so store with a far-future sentinel; the deny-list
            // sweep uses DateTimeOffset.MaxValue entries as "revoke until restart".
            _revokedJtis[jti] = DateTimeOffset.MaxValue;
        }

        public bool IsTokenRevoked(string jti)
        {
            if (string.IsNullOrWhiteSpace(jti)) return false;

            // Periodic sweep: remove entries whose JWT would have expired anyway (no longer need to deny)
            if (DateTimeOffset.UtcNow - _lastSweep > TimeSpan.FromMinutes(10))
            {
                _lastSweep = DateTimeOffset.UtcNow;
                foreach (var key in _revokedJtis.Keys.ToList())
                {
                    if (_revokedJtis.TryGetValue(key, out var exp) && exp != DateTimeOffset.MaxValue && exp < DateTimeOffset.UtcNow)
                        _revokedJtis.TryRemove(key, out _);
                }
            }

            return _revokedJtis.ContainsKey(jti);
        }

        public JwtSecurityToken GenerateJwt(string username, Role role, int? ttl = null)
        {
            var issuedUtc = DateTime.UtcNow;
            var expiresUtc = DateTime.UtcNow.AddMilliseconds(ttl ?? OptionsAtStartup.Web.Authentication.Jwt.Ttl);
            var jti = Guid.NewGuid().ToString("N");

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, jti),
                new Claim(ClaimTypes.Role, role.ToString()),
                new Claim("name", username),
                new Claim("iat", ((DateTimeOffset)issuedUtc).ToUnixTimeSeconds().ToString()),
            };

            var credentials = new SigningCredentials(JwtSigningKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Program.AppName,
                audience: Program.AppName,
                claims: claims,
                notBefore: issuedUtc,
                expires: expiresUtc,
                signingCredentials: credentials);

            return token;
        }
    }
}
