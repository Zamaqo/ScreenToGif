using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ScreenToGif.Cloud;

/// <summary>
/// AWS Signature Version 4, used by Cloudflare R2's S3-compatible API.
/// </summary>
public static class AwsSigV4
{
    public static string BuildAuthorization(string method, string canonicalUri, string canonicalQuery, IReadOnlyDictionary<string, string> headers, string payloadHash, string accessKeyId, string secretAccessKey, string dateStamp, string region, string service)
    {
        var signature = ComputeSignature(method, canonicalUri, canonicalQuery, headers, payloadHash, secretAccessKey, dateStamp, region, service);
        var signedHeaders = string.Join(";", NormalizeHeaders(headers).Select(header => header.Key));
        var credentialScope = CredentialScope(dateStamp, region, service);

        return $"AWS4-HMAC-SHA256 Credential={accessKeyId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    public static string ComputeSignature(string method, string canonicalUri, string canonicalQuery, IReadOnlyDictionary<string, string> headers, string payloadHash, string secretAccessKey, string dateStamp, string region, string service)
    {
        var canonicalRequest = BuildCanonicalRequest(method, canonicalUri, canonicalQuery, headers, payloadHash);
        var stringToSign = string.Join("\n", "AWS4-HMAC-SHA256", headers.First(header => header.Key.Equals("x-amz-date", StringComparison.OrdinalIgnoreCase)).Value.Trim(), CredentialScope(dateStamp, region, service), ToHex(Sha256(canonicalRequest)));

        var signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + secretAccessKey), dateStamp), region), service), "aws4_request");

        return ToHex(Hmac(signingKey, stringToSign));
    }

    public static string BuildCanonicalRequest(string method, string canonicalUri, string canonicalQuery, IReadOnlyDictionary<string, string> headers, string payloadHash)
    {
        var normalized = NormalizeHeaders(headers);
        var canonicalHeaders = string.Concat(normalized.Select(header => header.Key + ":" + header.Value + "\n"));
        var signedHeaders = string.Join(";", normalized.Select(header => header.Key));

        // canonicalHeaders already ends with a newline, so joining adds the required blank line before the signed headers.
        return string.Join("\n", method, canonicalUri, canonicalQuery ?? "", canonicalHeaders, signedHeaders, payloadHash);
    }

    private static List<KeyValuePair<string, string>> NormalizeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        return headers
            .Select(header => new KeyValuePair<string, string>(header.Key.Trim().ToLowerInvariant(), NormalizeHeaderValue(header.Value)))
            .OrderBy(header => header.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeHeaderValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;

        foreach (var character in trimmed)
        {
            if (character == ' ' || character == '\t')
            {
                if (previousWasSpace)
                    continue;

                builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString();
    }

    private static string CredentialScope(string dateStamp, string region, string service)
    {
        return dateStamp + "/" + region + "/" + service + "/aws4_request";
    }

    private static byte[] Hmac(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static byte[] Sha256(string data)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] data)
    {
        return Convert.ToHexString(data).ToLowerInvariant();
    }
}
