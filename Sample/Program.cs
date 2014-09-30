using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// 参照の追加
using AmazonS3Lib;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var key = "yours3Key";
            var secretKey = "yoursecretKet";
            var bucket = "yourBucketName";
            var syncDir = @"C:\S3";
            // C:\S3 フォルダにS3のデータを同期する
            var s3 = new S3(key, secretKey, bucket, syncDir);

            // 同期実行
            s3.Sync();
        }
    }
}
