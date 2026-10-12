using System;
using System.Collections.Generic;
using ScreenToGif.Cloud;
using Xunit;

namespace ScreenToGif.Test.Facts
{
    public class CloudflareR2Signature
    {
        [Fact]
        public void SignsTheAwsDocumentationExample()
        {
            // https://docs.aws.amazon.com/AmazonS3/latest/API/sig-v4-header-based-auth.html
            var headers = new Dictionary<string, string>
            {
                ["host"] = "examplebucket.s3.amazonaws.com",
                ["range"] = "bytes=0-9",
                ["x-amz-content-sha256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                ["x-amz-date"] = "20130524T000000Z"
            };
            const string payloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            var canonical = AwsSigV4.BuildCanonicalRequest("GET", "/test.txt", "", headers, payloadHash);
            var signature = AwsSigV4.ComputeSignature("GET", "/test.txt", "", headers, payloadHash, "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "20130524", "us-east-1", "s3");

            Assert.Contains("host;range;x-amz-content-sha256;x-amz-date", canonical);
            Assert.Equal("f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41", signature);
        }

        [Fact]
        public void BuildsTheAccountHost()
        {
            Assert.Equal("abc123.r2.cloudflarestorage.com", CloudflareR2.BuildHost("AbC123"));
            Assert.Equal("abc123.r2.cloudflarestorage.com", CloudflareR2.BuildHost("https://abc123.r2.cloudflarestorage.com/bucket"));
            Assert.Equal("abc123.eu.r2.cloudflarestorage.com", CloudflareR2.BuildHost("abc123.eu.r2.cloudflarestorage.com"));
        }

        [Fact]
        public void BuildsTheObjectKeyAndPublicLink()
        {
            Assert.Equal("recordings/my file.gif", CloudflareR2.BuildObjectKey(@"\recordings\", "my file.gif"));
            Assert.Equal("https://media.example.com/recordings/my%20file.gif", CloudflareR2.BuildPublicUrl("https://media.example.com/", "recordings/my file.gif"));
            Assert.Equal("image/gif", CloudflareR2.GetContentType("clip.gif"));
            Assert.Equal("video/mp4", CloudflareR2.GetContentType("clip.mp4"));
        }

        [Fact]
        public void RejectsAFolderThatClimbsOutOfTheBucket()
        {
            Assert.Throws<UploadException>(() => CloudflareR2.BuildObjectKey("../secret", "clip.gif"));
        }

        [Fact]
        public void AcceptsTheJurisdictionEndpointUrl()
        {
            Assert.Equal("abc123.r2.cloudflarestorage.com", CloudflareR2.BuildHost("https://abc123.r2.cloudflarestorage.com"));
        }
    }
}
