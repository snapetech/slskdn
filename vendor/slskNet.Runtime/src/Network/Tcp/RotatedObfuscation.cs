// <copyright file="RotatedObfuscation.cs" company="slskdN Team">
//     Copyright (c) slskdN Team.
//
//     This file is part of slskNet.Runtime, a modified version of Soulseek.NET.
//     Modified: Added Soulseek type-1 rotated obfuscation helpers.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, version 3.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
//
//     This program is distributed with Additional Terms pursuant to Section 7
//     of the GPLv3.  See the LICENSE file in the root directory of this
//     project for the complete terms and conditions.
//
//     SPDX-FileCopyrightText: slskdN Team
//     SPDX-License-Identifier: GPL-3.0-only
// </copyright>

namespace Soulseek.Network.Tcp
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    ///     Implements Soulseek type-1 rotated obfuscation frames.
    /// </summary>
    internal static class RotatedObfuscation
    {
        public const int Type = 1;

        public static byte[] Encode(byte[] input)
        {
            var keyBytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            return Encode(input, BitConverter.ToUInt32(keyBytes, 0));
        }

        public static byte[] Encode(byte[] input, uint key)
        {
            var output = new byte[4 + input.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(key), 0, output, 0, 4);
            Buffer.BlockCopy(input, 0, output, 4, input.Length);
            ApplyRotatedKeystream(output, 4, input.Length, key);
            return output;
        }

        public static byte[] Decode(byte[] input)
        {
            if (input.Length < 4)
            {
                throw new ArgumentException("Obfuscated frame must include a four-byte key", nameof(input));
            }

            var key = BitConverter.ToUInt32(input, 0);
            var output = new byte[input.Length - 4];
            Buffer.BlockCopy(input, 4, output, 0, output.Length);
            ApplyRotatedKeystream(output, 0, output.Length, key);
            return output;
        }

        private static void ApplyRotatedKeystream(byte[] buffer, int offset, int count, uint initialKey)
        {
            var key = initialKey;
            var end = offset + count;

            for (var position = offset; position < end; position += 4)
            {
                key = RotateLeft(key, 1);
                var keyBytes = BitConverter.GetBytes(key);
                var chunkLength = Math.Min(4, end - position);

                for (var index = 0; index < chunkLength; index++)
                {
                    buffer[position + index] ^= keyBytes[index];
                }
            }
        }

        private static uint RotateLeft(uint value, int count)
            => (value << count) | (value >> (32 - count));
    }
}
