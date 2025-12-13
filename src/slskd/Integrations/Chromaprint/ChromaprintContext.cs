// <copyright file="ChromaprintContext.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.Chromaprint
{
    using System;
    using System.Runtime.InteropServices;

    internal sealed class ChromaprintContext : SafeHandle
    {
        private ChromaprintContext()
            : base(IntPtr.Zero, true)
        {
        }

        public static ChromaprintContext Create(int algorithm)
        {
            try
            {
                var handle = Native.chromaprint_new(algorithm);
                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Unable to allocate Chromaprint context.");
                }

                var context = new ChromaprintContext();
                context.SetHandle(handle);
                return context;
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException("Chromaprint native library not found. Install 'chromaprint' (libchromaprint, chromaprint.dll, or libchromaprint.dylib) and ensure it is available on the PATH.", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException("Chromaprint native library is missing expected exports. Ensure the installed version matches the required API.", ex);
            }
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            Native.chromaprint_free(handle);
            return true;
        }

        public bool Start(int sampleRate, int channels)
        {
            return Native.chromaprint_start(handle, sampleRate, channels) != 0;
        }

        public bool Feed(ReadOnlySpan<short> samples)
        {
            if (samples.IsEmpty)
            {
                return true;
            }

            var ints = new int[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                ints[i] = samples[i];
            }

            var handle = GCHandle.Alloc(ints, GCHandleType.Pinned);
            try
            {
                var pointer = handle.AddrOfPinnedObject();
                return Native.chromaprint_feed(handle: this.handle, data: pointer, size: samples.Length) != 0;
            }
            finally
            {
                handle.Free();
            }
        }

        public bool Finish()
        {
            return Native.chromaprint_finish(handle) != 0;
        }

        public string? GetFingerprint()
        {
            if (Native.chromaprint_get_fingerprint(handle, out var pointer) == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringAnsi(pointer);
            }
            finally
            {
                Native.chromaprint_dealloc(pointer);
            }
        }

        private static class Native
        {
            private const string LibraryName = "chromaprint";

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr chromaprint_new(int algorithm);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void chromaprint_free(IntPtr context);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int chromaprint_start(IntPtr context, int sampleRate, int channels);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int chromaprint_feed(IntPtr handle, IntPtr data, int size);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int chromaprint_finish(IntPtr context);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int chromaprint_get_fingerprint(IntPtr context, out IntPtr fingerprint);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void chromaprint_dealloc(IntPtr fingerprint);
        }
    }
}

















