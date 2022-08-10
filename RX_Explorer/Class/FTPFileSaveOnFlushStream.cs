﻿using FluentFTP;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class FtpFileSaveOnFlushStream : VirtualSaveOnFlushBaseStream
    {
        private readonly string Path;
        private readonly FtpClientController Controller;

        protected override async Task FlushCoreAsync(CancellationToken CancelToken)
        {
            FtpPathAnalysis Analysis = new FtpPathAnalysis(Path);

            if (await Controller.RunCommandAsync((Client) => Client.FileExistsAsync(Analysis.RelatedPath, CancelToken)).ConfigureAwait(false))
            {
                await Controller.RunCommandAsync((Client) => Client.DeleteFileAsync(Analysis.RelatedPath, CancelToken).ConfigureAwait(false));
            }

            BaseStream.Seek(0, SeekOrigin.Begin);

            using (Stream TargetStream = await Controller.RunCommandAsync((Client) => Client.OpenWriteAsync(Analysis.RelatedPath, FtpDataType.Binary, BaseStream.Length, CancelToken)).ConfigureAwait(false))
            {
                await BaseStream.CopyToAsync(TargetStream, CancelToken: CancelToken).ConfigureAwait(false);
            }
        }

        public FtpFileSaveOnFlushStream(string Path, FtpClientController Controller, Stream BaseStream) : base(BaseStream)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Argument could not be null");
            }

            if (Controller == null)
            {
                throw new ArgumentNullException(nameof(Controller), "Argument could not be null");
            }

            this.Path = Path;
            this.Controller = Controller;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Controller.Dispose();
        }
    }
}
