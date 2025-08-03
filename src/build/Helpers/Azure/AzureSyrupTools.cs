using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using Nuke.Common;

namespace Helpers.Azure
{
    public class AzureSyrupTools
    {
        const string _INDEX_FILE = "syrup.index.json";
        const string _SYRUP_EXT = ".syrup";
        readonly CloudBlobContainer CloudBlobContainer;

        AzureSyrupTools(CloudBlobContainer cloudBlobContainer)
        {
            CloudBlobContainer = cloudBlobContainer;
        }

        public async Task<List<ReleaseInfo>> GetSyrupFiles()
        {
            var strings = new List<string>();
            var ret = new List<ReleaseInfo>();
            BlobContinuationToken blobContinuationToken = null;
            do
            {
                var results = await CloudBlobContainer.ListBlobsSegmentedAsync(null, blobContinuationToken);
                // Get the value of the continuation token returned by the listing call.
                blobContinuationToken = results.ContinuationToken;
                foreach (var item in results.Results)
                {
                    var ext = Path.GetExtension(item.Uri.AbsolutePath);
                    if (ext == _SYRUP_EXT) strings.Add(item.Uri.OriginalString);
                }
            } while (blobContinuationToken != null); // Loop while the continuation token is not null.

            var pathToContainer = CloudBlobContainer.Uri.ToString();

            foreach (var path in strings)
            {
                //Logger.Info($"BlobPath: {path}");
                var fileName = Path.GetFileName(path);
                //Logger.Info($"fileName: {path}");

                var blob = CloudBlobContainer.GetBlockBlobReference(fileName);
                var text = await blob.DownloadTextAsync();
                var info = JsonConvert.DeserializeObject<SyrupInfo>(text);
                var url = $"{pathToContainer}/{info.File}";
                var releaseInfo = new ReleaseInfo(info, url);
             
                ret.Add(releaseInfo);
            }

            return ret;
        }

        public async Task CreateSyrupFilesList(List<ReleaseInfo> syrupInfos)
        {
            var json = JsonConvert.SerializeObject(syrupInfos, Formatting.Indented);
            var blob = CloudBlobContainer.GetBlockBlobReference(_INDEX_FILE);
            await blob.UploadTextAsync(json);
        }

        public async Task UploadFiles(List<string> files)
        {
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var blockBlob = CloudBlobContainer.GetBlockBlobReference(fileName);

                using (var fileStream = File.OpenRead(file))
                {
                    await blockBlob.UploadFromStreamAsync(fileStream);
                }
            }
        }

        public static AzureSyrupTools Create(string connectionString, string containerName)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new NullReferenceException("ConnectionString to azure storage is empty");
            if (string.IsNullOrEmpty(containerName))
                throw new NullReferenceException("Container name in azure storage is empty");
            if (!CloudStorageAccount.TryParse(connectionString, out var storageAccount)) return null;
            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            if (cloudBlobClient == null) throw new NullReferenceException("CloudBlobClient is null");
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            var instance = new AzureSyrupTools(cloudBlobContainer);
            return instance;
        }

        public async Task RemoveSyrupFiles(List<ReleaseInfo> fileToRemove)
        {
            async Task<bool> DeleteFileInBlob(string file)
            {
                var blobFile = CloudBlobContainer.GetBlockBlobReference(file);
                return await blobFile.DeleteIfExistsAsync();
            }


            foreach (var syrupInfo in fileToRemove)
            {
                var mainFileName = syrupInfo.File;
                var syrupFileName = $"{mainFileName}{_SYRUP_EXT}";
                await DeleteFileInBlob(mainFileName);
                await DeleteFileInBlob(syrupFileName);
            }
        }
    }
}