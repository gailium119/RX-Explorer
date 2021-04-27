﻿using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class CompressionUtil
    {

        private static Encoding EncodingSetting = Encoding.Default;
        private delegate void ByteReadChangedEventHandler(ulong ByteRead);

        public static async Task CreateZipAsync(IEnumerable<string> SourceItemGroup, string NewZipPath, CompressionLevel ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in SourceItemGroup)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    TransformList.Add(Item);
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            await CreateZipAsync(TransformList, NewZipPath, ZipLevel, ProgressHandler);
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="SourceItemGroup">待压缩文件</param>
        /// <param name="NewZipPath">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        /// <returns>无</returns>
        public static async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, CompressionLevel Level, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.SizeRaw;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    ZipStrings.CodePage = EncodingSetting.CodePage;

                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                    using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                    {
                        OutputStream.SetLevel((int)Level);
                        OutputStream.UseZip64 = UseZip64.Dynamic;
                        OutputStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                        {
                                            ZipEntry NewEntry = new ZipEntry(File.Name)
                                            {
                                                DateTime = DateTime.Now,
                                                CompressionMethod = CompressionMethod.Deflated,
                                                Size = FileStream.Length
                                            };

                                            OutputStream.PutNextEntry(NewEntry);

                                            await FileStream.CopyToAsync(OutputStream, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                            }).ConfigureAwait(false);
                                        }

                                        OutputStream.CloseEntry();

                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await ZipFolderCore(Folder, OutputStream, Folder.Name, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task ZipFolderCore(FileSystemStorageFolder Folder, ZipOutputStream OutputStream, string BaseFolderName, ByteReadChangedEventHandler ByteReadHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildItemsAsync(true, true).ConfigureAwait(false);

            if (ItemList.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/");
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder InnerFolder:
                            {
                                ulong InnerFolderSize = 0;

                                await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{Item.Name}", ByteReadHandler: (ByteRead) =>
                                {
                                    InnerFolderSize = ByteRead;
                                    ByteReadHandler?.Invoke(CurrentPosition + ByteRead);
                                }).ConfigureAwait(false);

                                ByteReadHandler?.Invoke(CurrentPosition += InnerFolderSize);

                                break;
                            }
                        case FileSystemStorageFile InnerFile:
                            {
                                using (FileStream FileStream = await InnerFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{Item.Name}")
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream, ProgressHandler: (s, e) =>
                                    {
                                        ByteReadHandler?.Invoke(CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.SizeRaw));
                                    }).ConfigureAwait(false);
                                }

                                OutputStream.CloseEntry();

                                ByteReadHandler?.Invoke(CurrentPosition += Item.SizeRaw);

                                break;
                            }
                    }
                }
            }
        }

        public static async Task CreateGzipAsync(string Source, string NewZipPath, CompressionLevel ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Source) is FileSystemStorageFile File)
            {
                await CreateGzipAsync(File, NewZipPath, ZipLevel, ProgressHandler);
            }
            else
            {
                throw new FileNotFoundException("Could not found the file path");
            }
        }

        public static async Task CreateGzipAsync(FileSystemStorageFile Source, string NewZipPath, CompressionLevel Level, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                using (FileStream SourceFileStream = await Source.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                using (GZipOutputStream GZipStream = new GZipOutputStream(NewFileStream))
                {
                    GZipStream.SetLevel((int)Level);

                    await SourceFileStream.CopyToAsync(GZipStream, ProgressHandler: ProgressHandler).ConfigureAwait(false);
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        public static async Task CreateTarAsync(IEnumerable<string> SourceItemGroup, string NewZipPath, TarCompressionType TarType, CompressionLevel Level, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> TransformList = new List<FileSystemStorageItemBase>();

            foreach (string Path in SourceItemGroup)
            {
                if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    TransformList.Add(Item);
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            switch (TarType)
            {
                case TarCompressionType.None:
                    {
                        await CreateTarAsync(TransformList, NewZipPath, ProgressHandler);
                        break;
                    }
                case TarCompressionType.Gz:
                    {
                        await CreateTarGzipAsync(TransformList, NewZipPath, Level, ProgressHandler);
                        break;
                    }
                case TarCompressionType.Bz2:
                    {
                        await CreateTarBzip2Async(TransformList, NewZipPath, ProgressHandler);
                        break;
                    }
            }
        }

        private static async Task CreateTarBzip2Async(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.SizeRaw;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                    using (BZip2OutputStream OutputGzipStream = new BZip2OutputStream(NewFileStream))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(OutputGzipStream, EncodingSetting))
                    {
                        OutputGzipStream.IsStreamOwner = false;
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            OutputTarStream.PutNextEntry(NewEntry);

                                            await FileStream.CopyToAsync(OutputTarStream, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                            }).ConfigureAwait(false);
                                        }

                                        OutputTarStream.CloseEntry();

                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task CreateTarGzipAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, CompressionLevel Level, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Level == CompressionLevel.Undefine)
            {
                throw new ArgumentException("Undefine is not allowed in this function", nameof(Level));
            }

            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.SizeRaw;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                    using (GZipOutputStream OutputGzipStream = new GZipOutputStream(NewFileStream))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(OutputGzipStream, EncodingSetting))
                    {
                        OutputGzipStream.SetLevel((int)Level);
                        OutputGzipStream.IsStreamOwner = false;
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            OutputTarStream.PutNextEntry(NewEntry);

                                            await FileStream.CopyToAsync(OutputTarStream, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                            }).ConfigureAwait(false);
                                        }

                                        OutputTarStream.CloseEntry();

                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task CreateTarAsync(IEnumerable<FileSystemStorageItemBase> SourceItemGroup, string NewZipPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await FileSystemStorageItemBase.CreateAsync(NewZipPath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
            {
                ulong TotalSize = 0;
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                {
                    switch (StorageItem)
                    {
                        case FileSystemStorageFile File:
                            {
                                TotalSize += File.SizeRaw;
                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                TotalSize += await Folder.GetFolderSizeAsync().ConfigureAwait(false);
                                break;
                            }
                    }
                }

                if (TotalSize > 0)
                {
                    using (FileStream NewFileStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Exclusive).ConfigureAwait(false))
                    using (TarOutputStream OutputTarStream = new TarOutputStream(NewFileStream, EncodingSetting))
                    {
                        OutputTarStream.IsStreamOwner = false;

                        foreach (FileSystemStorageItemBase StorageItem in SourceItemGroup)
                        {
                            switch (StorageItem)
                            {
                                case FileSystemStorageFile File:
                                    {
                                        using (FileStream FileStream = await File.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                        {
                                            TarEntry NewEntry = TarEntry.CreateTarEntry(File.Name);
                                            NewEntry.ModTime = DateTime.Now;
                                            NewEntry.Size = FileStream.Length;

                                            OutputTarStream.PutNextEntry(NewEntry);

                                            await FileStream.CopyToAsync(OutputTarStream, ProgressHandler: (s, e) =>
                                            {
                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * File.SizeRaw)) * 100d / TotalSize), null));
                                            }).ConfigureAwait(false);
                                        }

                                        OutputTarStream.CloseEntry();

                                        CurrentPosition += File.SizeRaw;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                                case FileSystemStorageFolder Folder:
                                    {
                                        ulong InnerFolderSize = 0;

                                        await TarFolderCore(Folder, OutputTarStream, Folder.Name, (ByteRead) =>
                                        {
                                            InnerFolderSize = ByteRead;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + ByteRead) * 100d / TotalSize), null));
                                        }).ConfigureAwait(false);

                                        CurrentPosition += InnerFolderSize;
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));

                                        break;
                                    }
                            }
                        }

                        await OutputTarStream.FlushAsync().ConfigureAwait(false);
                    }
                }
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static async Task TarFolderCore(FileSystemStorageFolder Folder, TarOutputStream OutputStream, string BaseFolderName, ByteReadChangedEventHandler ByteReadHandler = null)
        {
            List<FileSystemStorageItemBase> ItemList = await Folder.GetChildItemsAsync(true, true).ConfigureAwait(false);

            if (ItemList.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/");
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                ulong CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder InnerFolder:
                            {
                                ulong InnerFolderSize = 0;

                                await TarFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}", ByteReadHandler: (ByteRead) =>
                                {
                                    InnerFolderSize = ByteRead;
                                    ByteReadHandler?.Invoke(CurrentPosition + ByteRead);
                                }).ConfigureAwait(false);

                                ByteReadHandler?.Invoke(CurrentPosition += InnerFolderSize);

                                break;
                            }
                        case FileSystemStorageFile InnerFile:
                            {
                                using (FileStream FileStream = await InnerFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                                {
                                    TarEntry NewEntry = TarEntry.CreateTarEntry($"{BaseFolderName}/{InnerFile.Name}");
                                    NewEntry.ModTime = DateTime.Now;
                                    NewEntry.Size = FileStream.Length;

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream, ProgressHandler: (s, e) =>
                                    {
                                        ByteReadHandler?.Invoke(CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * InnerFile.SizeRaw));
                                    }).ConfigureAwait(false);
                                }

                                OutputStream.CloseEntry();

                                ByteReadHandler?.Invoke(CurrentPosition += InnerFile.SizeRaw);

                                break;
                            }
                    }
                }
            }
        }

        public static async Task ExtractAllAsync(IEnumerable<string> SourceItemGroup, string BaseDestPath, bool CreateFolder, ProgressChangedEventHandler ProgressHandler)
        {
            ulong TotalSize = 0;
            ulong CurrentPosition = 0;

            List<FileSystemStorageFile> TransformList = new List<FileSystemStorageFile>();

            foreach (string FileItem in SourceItemGroup)
            {
                if (await FileSystemStorageItemBase.OpenAsync(FileItem).ConfigureAwait(false) is FileSystemStorageFile File)
                {
                    TransformList.Add(File);
                    TotalSize += File.SizeRaw;
                }
                else
                {
                    throw new FileNotFoundException("Could not found the file or path is a directory");
                }
            }

            if (TotalSize == 0)
            {
                return;
            }

            foreach (FileSystemStorageFile File in TransformList)
            {
                string DestPath = BaseDestPath;

                //如果解压到独立文件夹,则要额外创建目录
                if (CreateFolder)
                {
                    string NewFolderName = File.Name.Contains(".tar.", StringComparison.OrdinalIgnoreCase) ? File.Name.Split(".")[0] : Path.GetFileNameWithoutExtension(File.Name);

                    if (!string.IsNullOrEmpty(NewFolderName))
                    {
                        DestPath = Path.Combine(BaseDestPath, NewFolderName);
                        await FileSystemStorageItemBase.CreateAsync(DestPath, StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false);
                    }
                }

                ReaderOptions ReadOptions = new ReaderOptions();
                ReadOptions.ArchiveEncoding.Default = EncodingSetting;

                using (FileStream InputStream = await File.GetFileStreamFromFileAsync(AccessMode.Read))
                using (IReader Reader = ReaderFactory.Open(InputStream, ReadOptions))
                {
                    while (Reader.MoveToNextEntry())
                    {
                        if (Reader.Entry.IsDirectory)
                        {
                            await FileSystemStorageItemBase.CreateAsync(Path.Combine(DestPath, Reader.Entry.Key.Replace("/", @"\").TrimEnd('\\')), StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false);
                            continue;
                        }

                        string DestFileName = string.Empty;

                        if (Reader.Entry.CompressionType == SharpCompress.Common.CompressionType.GZip)
                        {
                            DestFileName = Path.Combine(DestPath, Path.GetFileNameWithoutExtension(File.Name));
                        }
                        else
                        {
                            string[] PathList = (Reader.Entry.Key?.Replace("/", @"\").Split(@"\")) ?? Array.Empty<string>();

                            string LastFolder = DestPath;

                            for (int i = 0; i < PathList.Length - 1; i++)
                            {
                                LastFolder = Path.Combine(LastFolder, PathList[i]);
                                await FileSystemStorageItemBase.CreateAsync(LastFolder, StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false);
                            }

                            DestFileName = Path.Combine(LastFolder, PathList.LastOrDefault());
                        }

                        if (await FileSystemStorageItemBase.CreateAsync(DestFileName, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile NewFile)
                        {
                            using (FileStream OutputStream = await NewFile.GetFileStreamFromFileAsync(AccessMode.Write))
                            using (EntryStream EntryStream = Reader.OpenEntryStream())
                            {
                                await EntryStream.CopyToAsync(OutputStream, Reader.Entry.Size, (s, e) =>
                                {
                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * Reader.Entry.CompressedSize)) * 100d / TotalSize), null));
                                });

                                CurrentPosition += Convert.ToUInt64(Reader.Entry.CompressedSize);
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(CurrentPosition * 100d / TotalSize), null));
                            }
                        }
                    }
                }
            }
        }

        public static void SetEncoding(Encoding Encoding)
        {
            EncodingSetting = Encoding;
        }
    }
}
