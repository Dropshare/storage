﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storage.Net.Blob.Files
{
   /// <summary>
   /// Blob storage implementation which uses local file system directory
   /// </summary>
   public class DirectoryFilesBlobStorage : IBlobStorage
   {
      private readonly DirectoryInfo _directory;
      private static readonly string FsPathSeparator = new string(Path.DirectorySeparatorChar, 1);

      /// <summary>
      /// Creates an instance in a specific disk directory
      /// <param name="directory">Root directory</param>
      /// </summary>
      public DirectoryFilesBlobStorage(DirectoryInfo directory)
      {
         _directory = directory;
      }

      /// <summary>
      /// Returns the list of blob names in this storage, optionally filtered by prefix
      /// </summary>
      public IEnumerable<string> List(string prefix)
      {
         GenericValidation.CheckBlobPrefix(prefix);

         if(!_directory.Exists) return null;

         string[] allIds = _directory.GetFiles("*", SearchOption.AllDirectories).Select(ToId).ToArray();

         if (string.IsNullOrEmpty(prefix)) return allIds;

         string wildcard = prefix + "*";
         return allIds.Where(id => id.MatchesWildcard(wildcard));
      }

      /// <summary>
      /// Returns the list of blob names in this storage, optionally filtered by prefix
      /// </summary>
      public Task<IEnumerable<string>> ListAsync(string prefix)
      {
         return Task.FromResult(List(prefix));
      }


      private string ToId(FileInfo fi)
      {
         string name = fi.FullName.Substring(_directory.FullName.Length + 1);

         return name.Replace(Path.DirectorySeparatorChar, StoragePath.PathSeparator);
      }

      /// <summary>
      /// Deletes blob file
      /// </summary>
      /// <param name="id"></param>
      public void Delete(string id)
      {
         GenericValidation.CheckBlobId(id);

         string path = GetFilePath(id);
         if(File.Exists(path)) File.Delete(path);
      }

      /// <summary>
      /// Deletes blob file
      /// </summary>
      /// <param name="id"></param>
      public Task DeleteAsync(string id)
      {
         Delete(id);

         return Task.FromResult(true);
      }

      /// <summary>
      /// Writes blob to file
      /// </summary>
      public void UploadFromStream(string id, Stream sourceStream)
      {
         GenericValidation.CheckBlobId(id);
         if(sourceStream == null) throw new ArgumentNullException(nameof(sourceStream));

         using(Stream target = CreateStream(id))
         {
            sourceStream.CopyTo(target);
         }
      }

      /// <summary>
      /// Writes blob to file
      /// </summary>
      public async Task UploadFromStreamAsync(string id, Stream sourceStream)
      {
         GenericValidation.CheckBlobId(id);
         if (sourceStream == null) throw new ArgumentNullException(nameof(sourceStream));

         using (Stream target = CreateStream(id))
         {
            await sourceStream.CopyToAsync(target);
         }
      }

      /// <summary>
      /// Append chunk to file
      /// </summary>
      public void AppendFromStream(string id, Stream chunkStream)
      {
         GenericValidation.CheckBlobId(id);
         if (chunkStream == null) throw new ArgumentNullException(nameof(chunkStream));

         using (Stream target = CreateStream(id, false))
         {
            chunkStream.CopyTo(target);
         }
      }

      /// <summary>
      /// Append chunk to file
      /// </summary>
      public async Task AppendFromStreamAsync(string id, Stream chunkStream)
      {
         GenericValidation.CheckBlobId(id);
         if (chunkStream == null) throw new ArgumentNullException(nameof(chunkStream));

         using (Stream target = CreateStream(id, false))
         {
            await chunkStream.CopyToAsync(target);
         }
      }

      /// <summary>
      /// Reads blob from file and writes to the target stream
      /// </summary>
      public void DownloadToStream(string id, Stream targetStream)
      {
         GenericValidation.CheckBlobId(id);
         if (targetStream == null) throw new ArgumentNullException(nameof(targetStream));

         using(Stream source = OpenStream(id))
         {
            if(source == null)
            {
               throw new StorageException(ErrorCode.NotFound, null);
            }

            source.CopyTo(targetStream);
            targetStream.Flush();
         }
      }

      /// <summary>
      /// Reads blob from file and writes to the target stream
      /// </summary>
      public async Task DownloadToStreamAsync(string id, Stream targetStream)
      {
         GenericValidation.CheckBlobId(id);
         if (targetStream == null) throw new ArgumentNullException(nameof(targetStream));

         using (Stream source = OpenStream(id))
         {
            if (source == null)
            {
               throw new StorageException(ErrorCode.NotFound, null);
            }

            await source.CopyToAsync(targetStream);
            await targetStream.FlushAsync();
         }
      }

      /// <summary>
      /// Opens the blob as a readable stream
      /// </summary>
      public Stream OpenStreamToRead(string id)
      {
         GenericValidation.CheckBlobId(id);

         return OpenStream(id);
      }

      /// <summary>
      /// Opens the blob as a readable stream
      /// </summary>
      public Task<Stream> OpenStreamToReadAsync(string id)
      {
         return Task.FromResult(OpenStreamToRead(id));
      }


      /// <summary>
      /// Checks if file exists
      /// </summary>
      public bool Exists(string id)
      {
         GenericValidation.CheckBlobId(id);

         return File.Exists(GetFilePath(id));
      }

      /// <summary>
      /// Checks if file exists
      /// </summary>
      public Task<bool> ExistsAsync(string id)
      {
         return Task.FromResult(Exists(id));
      }

      /// <summary>
      /// Gets blob metadata
      /// </summary>
      public BlobMeta GetMeta(string id)
      {
         GenericValidation.CheckBlobId(id);

         string path = GetFilePath(id);

         if (!File.Exists(path)) return null;

         var fi = new FileInfo(path);

         return new BlobMeta(
            fi.Length);
      }

      /// <summary>
      /// Gets blob metadata
      /// </summary>
      public Task<BlobMeta> GetMetaAsync(string id)
      {
         return Task.FromResult(GetMeta(id));
      }

      private string GetFilePath(string id)
      {
         GenericValidation.CheckBlobId(id);

         //id can contain path separators
         string[] parts = id.Split(StoragePath.PathSeparator);
         string name = parts[parts.Length - 1].SanitizePath();
         DirectoryInfo dir;
         if(parts.Length == 1)
         {
            dir = _directory;
         }
         else
         {
            string extraPath = string.Join(FsPathSeparator, parts, 0, parts.Length - 1);

            string fullPath = Path.Combine(_directory.FullName, extraPath);

            dir = new DirectoryInfo(fullPath);
            if (!dir.Exists) dir.Create();
         }

         return Path.Combine(dir.FullName, name.SanitizePath());
      }

      private Stream CreateStream(string id, bool overwrite = true)
      {
         GenericValidation.CheckBlobId(id);
         if (!_directory.Exists) _directory.Create();
         string path = GetFilePath(id);

         Stream s = overwrite ? File.Create(path) : File.OpenWrite(path);
         s.Seek(0, SeekOrigin.End);
         return s;
      }

      private Stream OpenStream(string id)
      {
         GenericValidation.CheckBlobId(id);
         string path = GetFilePath(id);
         if(!File.Exists(path)) return null;

         return File.OpenRead(path);
      }

   }
}