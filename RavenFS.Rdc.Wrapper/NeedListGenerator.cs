﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RavenFS.Rdc.Wrapper
{
    public class NeedListGenerator : CriticalFinalizerObject, IDisposable
    {
        private readonly ReaderWriterLockSlim _disposerLock = new ReaderWriterLockSlim();
        private bool _disposed;
        private readonly ISignatureRepository _seedSignatureRepository;
        private readonly ISignatureRepository _sourceSignatureRepository;

        private readonly IRdcLibrary _rdcLibrary;
        private const int ComparatorBufferSize = 0x8000000;
        private const int InputBufferSize = 0x100000;

        public NeedListGenerator(ISignatureRepository seedSignatureRepository, ISignatureRepository sourceSignatureRepository)
        {
            _rdcLibrary = (IRdcLibrary)new RdcLibrary();
            _seedSignatureRepository = seedSignatureRepository;
            _sourceSignatureRepository = sourceSignatureRepository;
        }

        public IList<RdcNeed> CreateNeedsList(SignatureInfo seedSignature, SignatureInfo sourceSignature)
        {
            var result = new List<RdcNeed>();
            using (var seedStream = _seedSignatureRepository.GetContentForReading(seedSignature.Name))
            using (var sourceStream = _sourceSignatureRepository.GetContentForReading(sourceSignature.Name))
            {
                var fileReader = (IRdcFileReader)new RdcFileReader(seedStream);
                IRdcComparator comparator;
                if (_rdcLibrary.CreateComparator(fileReader, ComparatorBufferSize, out comparator) != 0)
                {
                    throw new RdcException("Cannot create comparator");
                }

                var inputBuffer = new RdcBufferPointer();
                inputBuffer.Size = 0;
                inputBuffer.Used = 0;
                inputBuffer.Data = Marshal.AllocCoTaskMem(InputBufferSize + 16); // Completely don't know why 16

                var outputBuffer = Marshal.AllocCoTaskMem(
                    Marshal.SizeOf(typeof(RdcNeed)) * 256);

                try
                {
                    var eofInput = false;
                    var eofOutput = false;
                    var outputPointer = new RdcNeedPointer();

                    while (!eofOutput)
                    {
                        var bytesRead = 0;
                        if (inputBuffer.Size == inputBuffer.Used && !eofInput)
                        {                            
                            try
                            {
                                bytesRead = RdcBufferTools.IntPtrCopy(sourceStream, inputBuffer.Data, InputBufferSize);
                            }
                            catch (Exception ex)
                            {
                                throw new RdcException("Failed to read from the source stream.", ex);
                            }

                            inputBuffer.Size = (uint)bytesRead;
                            inputBuffer.Used = 0;

                            if (bytesRead < InputBufferSize)
                            {
                                eofInput = true;
                            }
                        }

                        // Initialize our output needs array
                        outputPointer.Size = 256;
                        outputPointer.Used = 0;
                        outputPointer.Data = outputBuffer;

                        RdcError error;

                        var hr = comparator.Process(eofInput, ref eofOutput, ref inputBuffer, ref outputPointer, out error);

                        if (hr != 0)
                        {
                            throw new RdcException("Failed to process the signature block!", hr, error);
                        }

                        // Convert the stream to a Needs array.
                        var needs = GetRdcNeedList(outputPointer);
                        result.AddRange(needs);
                    }
                }
                finally
                {
                    // Free our resources
                    if (outputBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(outputBuffer);
                    }

                    if (inputBuffer.Data != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(inputBuffer.Data);
                    }
                }
                return result;
            }
        }

        private RdcNeed[] GetRdcNeedList(RdcNeedPointer pointer)
        {
            var result = new RdcNeed[pointer.Used];

            var ptr = pointer.Data;
            var needSize = Marshal.SizeOf(typeof(RdcNeed));

            // Get our native needs pointer 
            // and deserialize to our managed 
            // RdcNeed array.
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = (RdcNeed)Marshal.PtrToStructure(ptr, typeof(RdcNeed));

                // Advance the intermediate pointer
                // to our next RdcNeed struct.
                ptr = (IntPtr)(ptr.ToInt32() + needSize);
            }
            return result;
        }

        ~NeedListGenerator()
        {
            try
            {
                Trace.WriteLine(
                    "~NeedListGenerator: Disposing esent resources from finalizer! You should call Dispose() instead!");
                DisposeInternal();
            }
            catch (Exception exception)
            {
                try
                {
                    Trace.WriteLine("Failed to dispose esent instance from finalizer because: " + exception);
                }
                catch
                {
                }
            }
        }        

        public void Dispose()
        {
            _disposerLock.EnterWriteLock();
            try
            {
                if (_disposed)
                    return;
                GC.SuppressFinalize(this);
                DisposeInternal();
            }
            finally
            {
                _disposed = true;
                _disposerLock.ExitWriteLock();
            }            
        }

        private void DisposeInternal()
        {
            if (_rdcLibrary != null)
            {
                Marshal.ReleaseComObject(_rdcLibrary);
            }
        }
    }
}
