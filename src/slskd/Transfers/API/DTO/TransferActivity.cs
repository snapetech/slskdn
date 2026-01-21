// <copyright file="TransferActivity.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.API
{
    using System;
    using Soulseek;

    /// <summary>
    ///     Transfer activity event for real-time updates.
    /// </summary>
    public class TransferActivity
    {
        /// <summary>
        ///     Gets the timestamp of the activity.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Gets the transfer direction.
        /// </summary>
        public TransferDirection Direction { get; set; }

        /// <summary>
        ///     Gets the username involved in the transfer.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets the filename being transferred.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets the previous transfer state.
        /// </summary>
        public TransferStates PreviousState { get; set; }

        /// <summary>
        ///     Gets the current transfer state.
        /// </summary>
        public TransferStates State { get; set; }

        /// <summary>
        ///     Gets the transfer size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets the bytes transferred.
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        ///     Gets the average transfer speed.
        /// </summary>
        public double AverageSpeed { get; set; }

        /// <summary>
        ///     Gets the transfer completion percentage.
        /// </summary>
        public double PercentComplete { get; set; }

        /// <summary>
        ///     Creates a TransferActivity from a Soulseek Transfer and state change event.
        /// </summary>
        /// <param name="transfer">The Soulseek transfer.</param>
        /// <param name="previousState">The previous transfer state.</param>
        /// <returns>A TransferActivity instance.</returns>
        public static TransferActivity FromTransferStateChange(Soulseek.Transfer transfer, TransferStates previousState)
        {
            return new TransferActivity
            {
                Direction = transfer.Direction,
                Username = transfer.Username,
                Filename = transfer.Filename,
                PreviousState = previousState,
                State = transfer.State,
                Size = transfer.Size,
                BytesTransferred = transfer.BytesTransferred,
                AverageSpeed = transfer.AverageSpeed,
                PercentComplete = transfer.PercentComplete,
            };
        }
    }
}

