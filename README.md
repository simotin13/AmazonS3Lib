AmazonS3Lib
===========

.Net S3 Client Library For Sync

<h2>■How to Use</h2>
　・All you have to do is link "AmazonS3Lib", set your key, secretKey bucketname syncDir as string, and call Sync method!
<pre>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            // sync C:\\S3 Foloder ⇔ AWS S3 bucket
            var syncDir = @"C:\S3";
            var s3 = new S3(key, secretKey, bucket, syncDir);

            s3.Sync();
        }
    }
}

</pre>

<h2>■Solusion File</h2>
・VisualStudio2010 or later

<h2>■How works this library</h2>
1.Download All S3 Objects.

2.upload All local files and folders.

3.If same File exists, Check Timestamp, and newer file will synced.
