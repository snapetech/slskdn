// <copyright file="MeshMessages.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.Mesh.Messages
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    ///     Mesh protocol message types.
    /// </summary>
    public enum MeshMessageType
    {
        /// <summary>Initial handshake message.</summary>
        Hello = 1,

        /// <summary>Request delta entries since sequence ID.</summary>
        ReqDelta = 2,

        /// <summary>Push delta entries response.</summary>
        PushDelta = 3,

        /// <summary>Request specific hash key.</summary>
        ReqKey = 4,

        /// <summary>Response with hash key lookup result.</summary>
        RespKey = 5,

        /// <summary>Acknowledge receipt of entries.</summary>
        Ack = 6,
    }

    /// <summary>
    ///     Base class for mesh messages.
    /// </summary>
    public abstract class MeshMessage
    {
        /// <summary>
        ///     Gets the message type.
        /// </summary>
        [JsonPropertyName("type")]
        public abstract MeshMessageType Type { get; }

        /// <summary>
        ///     Gets or sets the protocol version.
        /// </summary>
        [JsonPropertyName("proto_version")]
        public int ProtocolVersion { get; set; } = 1;

        /// <summary>
        ///     Gets or sets the sender's Ed25519 public key (Base64-encoded).
        ///     SECURITY: Used for message authentication.
        /// </summary>
        [JsonPropertyName("public_key")]
        public string PublicKey { get; set; }

        /// <summary>
        ///     Gets or sets the Ed25519 signature of the message (Base64-encoded).
        ///     SECURITY: Signature covers: type|timestamp|payload_json
        /// </summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        /// <summary>
        ///     Gets or sets the message timestamp (Unix milliseconds).
        ///     SECURITY: Used for replay protection and signature payload.
        /// </summary>
        [JsonPropertyName("timestamp_ms")]
        public long TimestampUnixMs { get; set; }
    }

    /// <summary>
    ///     Initial handshake message exchanged when mesh-capable peers connect.
    /// </summary>
    public class MeshHelloMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.Hello;

        /// <summary>
        ///     Gets or sets the client's username.
        /// </summary>
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        /// <summary>
        ///     Gets or sets the client version string.
        /// </summary>
        [JsonPropertyName("client_version")]
        public string ClientVersion { get; set; }

        /// <summary>
        ///     Gets or sets the highest local sequence ID.
        /// </summary>
        [JsonPropertyName("latest_seq_id")]
        public long LatestSeqId { get; set; }

        /// <summary>
        ///     Gets or sets the total hash count (for sizing).
        /// </summary>
        [JsonPropertyName("hash_count")]
        public int HashCount { get; set; }
    }

    /// <summary>
    ///     Request entries newer than a specified sequence ID.
    /// </summary>
    public class MeshReqDeltaMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.ReqDelta;

        /// <summary>
        ///     Gets or sets the sequence ID to start from (exclusive).
        /// </summary>
        [JsonPropertyName("since_seq_id")]
        public long SinceSeqId { get; set; }

        /// <summary>
        ///     Gets or sets the maximum entries to return.
        /// </summary>
        [JsonPropertyName("max_entries")]
        public int MaxEntries { get; set; } = 1000;
    }

    /// <summary>
    ///     Push delta entries to a peer.
    /// </summary>
    public class MeshPushDeltaMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.PushDelta;

        /// <summary>
        ///     Gets or sets the entries being pushed.
        /// </summary>
        [JsonPropertyName("entries")]
        public List<MeshHashEntry> Entries { get; set; } = new();

        /// <summary>
        ///     Gets or sets the sender's latest sequence ID.
        /// </summary>
        [JsonPropertyName("latest_seq_id")]
        public long LatestSeqId { get; set; }

        /// <summary>
        ///     Gets or sets whether there are more entries available.
        /// </summary>
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    /// <summary>
    ///     Request a specific hash key lookup.
    /// </summary>
    public class MeshReqKeyMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.ReqKey;

        /// <summary>
        ///     Gets or sets the FLAC key to look up.
        /// </summary>
        [JsonPropertyName("flac_key")]
        public string FlacKey { get; set; }
    }

    /// <summary>
    ///     Response to a key lookup request.
    /// </summary>
    public class MeshRespKeyMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.RespKey;

        /// <summary>
        ///     Gets or sets the FLAC key that was requested.
        /// </summary>
        [JsonPropertyName("flac_key")]
        public string FlacKey { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the key was found.
        /// </summary>
        [JsonPropertyName("found")]
        public bool Found { get; set; }

        /// <summary>
        ///     Gets or sets the hash entry (if found).
        /// </summary>
        [JsonPropertyName("entry")]
        public MeshHashEntry Entry { get; set; }
    }

    /// <summary>
    ///     Acknowledge receipt of entries.
    /// </summary>
    public class MeshAckMessage : MeshMessage
    {
        /// <inheritdoc/>
        public override MeshMessageType Type => MeshMessageType.Ack;

        /// <summary>
        ///     Gets or sets the number of entries merged.
        /// </summary>
        [JsonPropertyName("merged_count")]
        public int MergedCount { get; set; }

        /// <summary>
        ///     Gets or sets the receiver's latest sequence ID after merge.
        /// </summary>
        [JsonPropertyName("latest_seq_id")]
        public long LatestSeqId { get; set; }
    }

    /// <summary>
    ///     A hash entry exchanged via mesh sync.
    /// </summary>
    public class MeshHashEntry
    {
        /// <summary>
        ///     Gets or sets the sequence ID (from sender's perspective).
        /// </summary>
        [JsonPropertyName("seq_id")]
        public long SeqId { get; set; }

        /// <summary>
        ///     Gets or sets the FLAC key (64-bit truncated hash).
        /// </summary>
        [JsonPropertyName("flac_key")]
        public string FlacKey { get; set; }

        /// <summary>
        ///     Gets or sets the SHA256 hash of first 32KB bytes.
        /// </summary>
        [JsonPropertyName("byte_hash")]
        public string ByteHash { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets the packed metadata flags.
        /// </summary>
        [JsonPropertyName("meta_flags")]
        public int? MetaFlags { get; set; }
    }
}


