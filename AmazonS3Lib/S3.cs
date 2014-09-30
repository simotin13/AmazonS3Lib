using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Amazon;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.Web;

namespace AmazonS3Lib
{
    public class S3
    {
        private IAmazonS3 _s3Client;
        private string _bucket;
        private List<S3Object> _s3Objects;
        private string _syncDir;

        // 隠しファイル対応
        // word,excel等の一時ファイル(~$)は処理しない
        private string[] ExclusiveFileNamePrefix = new string[] { @"~$" };

        // 同期処理対象ファイル拡張子
        private string[] FilterFileExt = new string[] { ".pdf", ".xls", ".xlsx", ".doc", ".docx", ".ppt", ".pptx", ".txt", ".log", ".csv", ".jpg", ".jpeg", ".gif", ".png" };

        public S3(String accesskey, String secretAccessKey, string bucket, string syncDir)
        {
            var config = new AmazonS3Config();
            config.RegionEndpoint = RegionEndpoint.APNortheast1;
            _bucket = bucket;
            _s3Client = AWSClientFactory.CreateAmazonS3Client(accesskey, secretAccessKey, config);
            _syncDir = syncDir;
        }

        public void DeleteDir(string localDirName)
        {
            var key = getS3DirKey(localDirName);
            _s3Client.DeleteObject(new DeleteObjectRequest { BucketName = _bucket, Key = key });
        }

        public void Delete(string localfileName)
        {
            var key = getS3Key(localfileName);
            _s3Client.DeleteObject(new DeleteObjectRequest { BucketName = _bucket, Key = key });
        }

        public void Upload(string localfileName)
        {
            var localFilePath = String.Format(@"{0}\{1}", _syncDir, localfileName);
            var key = getS3Key(localFilePath);
            upload(localFilePath, key);
        }

        public void UpdateS3ObjectsInfo()
        {
            _s3Objects = listObjects();
        }

        private List<S3Object> listObjects()
        {
            return _s3Client.ListObjects(new ListObjectsRequest() { BucketName = _bucket }).S3Objects;
        }

        public void Sync()
        {
            _s3Objects = listObjects();
            var localFiles = Directory.GetFiles(_syncDir, "*", SearchOption.AllDirectories);
            var localDirs = Directory.GetDirectories(_syncDir, "*", SearchOption.AllDirectories);

            foreach (var s3obj in _s3Objects)
            {
                var localPath = getLocalPath(s3obj.Key);
                if (s3obj.Key.EndsWith("/"))
                {
                    // dir → Create Local Dir
                    Directory.CreateDirectory(localPath);
                }
                else
                {
                    // file
                    // check exist and download, if S3 file is newer or not exitst.
                    if (File.Exists(localPath))
                    {
                        if (File.GetLastWriteTime(localPath) < s3obj.LastModified)
                        {
                            // s3 file is newer → download s3 file.
                            if (checkFileName(Path.GetFileName(s3obj.Key)))
                            {
                                download(s3obj.Key, localPath);
                            }
                        }
                        else
                        {
                            // local file is newer → upload local file.
                            var key = getS3Key(localPath);
                            if (checkFileName(s3obj.Key))
                            {
                                upload(localPath, key);
                            }
                        }
                    }
                    else
                    {
                        // Not exsist on local → download s3 file.
                        if (checkFileName(Path.GetFileName(s3obj.Key)))
                        {
                            download(s3obj.Key, localPath);
                        }
                    }
                }
            }

            // ダウンロード前のファイルでS3にないものがあればアップロードする
            foreach (var localfile in localFiles)
            {
                var needUpload = true;
                foreach (var s3obj in _s3Objects)
                {
                    var s3filename = getLocalPath(s3obj.Key);
                    if (s3filename == localfile)
                    {
                        needUpload = false;
                        continue;
                    }
                }
                if (needUpload)
                {
                    if (checkFile(localfile))
                    {
                        var key = getS3Key(localfile);
                        upload(localfile, key);
                    }
                }
            }

            foreach (var localDir in localDirs)
            {
                var entryCount = Directory.GetFileSystemEntries(localDir).Count();
                if (entryCount == 0)
                {
                    // create dir if empty
                    var key = getS3DirKey(localDir);
                    createDir(key);
                }
            }
        }

        private Boolean checkFile(string localfile)
        {
            if (checkFileName(localfile) == false) { return false; }
            if (checkFileExt(localfile) == false) { return false; }
            if (checkFileAttribute(localfile) == false) { return false; }

            return true;
        }

        private Boolean checkFileName(string filePath)
        {
            foreach (var prefix in ExclusiveFileNamePrefix)
            {
                if (filePath.StartsWith(prefix) == true)
                {
                    return false;
                }
            }
            return true;
        }
        private Boolean checkFileExt(string filePath)
        {
            foreach (var filterExt in FilterFileExt)
            {
                var fileExt = Path.GetExtension(filePath);
                if (fileExt == filterExt)
                {
                    return true;
                }
                if (fileExt == filterExt.ToUpper())
                {
                    return true;
                }
            }
            return false;
        }
        private Boolean checkFileAttribute(string filePath)
        {
            var attr = File.GetAttributes(filePath);
            if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden) { return false; }
            if ((attr & FileAttributes.Temporary) == FileAttributes.Temporary) { return false; }

            return true;
        }
        private string getLocalPath(string s3Path)
        {
            return String.Format(@"{0}\{1}", _syncDir, s3Path.Replace(@"/", @"\"));
        }

        private string getS3Key(string locaPath)
        {
            return locaPath.Replace(_syncDir + @"\", "").Replace(@"\", @"/");
        }
        private string getS3DirKey(string localPath)
        {
            return getS3Key(localPath) + @"/";
        }

        private void upload(string localFilePath, string key)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                FilePath = localFilePath,
                ContentType = "application/octet-stream"
            };
            try
            {
                _s3Client.PutObject(putRequest);
            }
            catch (IOException ex)
            {
                // Local file opened → do nothing
            }
        }
        private void createDir(string key)
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                ContentType = "application/octet-stream"
            };
            _s3Client.PutObject(putRequest);
        }

        private void download(string key, string localPath)
        {
            GetObjectRequest getRequest = new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key
            };
            var response = _s3Client.GetObject(getRequest);
            response.WriteResponseStreamToFile(localPath);
        }
    }
}
