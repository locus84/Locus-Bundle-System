using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using BundleSystem;
using UnityEngine;

namespace BundleSystem
{
    public class LocusAssetbundleUploaderExtension : MonoBehaviour
    {
        
        public static void UploadToS3Bucket(AssetbundleBuildSettings settings)
        {
            var s3Client = new AmazonS3Client(RegionEndpoint.EUCentral1);
            Debug.LogError("UploadDirAsync Start!");
            UploadDirAsync(s3Client, settings.RemoteOutputPath, "moba-prototype-yaml-bucket");
            Debug.LogError("UploadDirAsync End!");
        }

        private static void UploadDirAsync(IAmazonS3 s3Client, string dirPath, string bucketName)
        {
            try
            {
                var dirTransferUtility = new TransferUtility();
                var request = new TransferUtilityUploadDirectoryRequest
                {
                    BucketName = bucketName,
                    Directory = dirPath,
                    SearchOption = SearchOption.AllDirectories,
                    CannedACL = S3CannedACL.PublicRead
                };
                dirTransferUtility.UploadDirectory(request);
            }
            catch (AmazonS3Exception e)
            {
                Debug.LogError(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }
    }
}