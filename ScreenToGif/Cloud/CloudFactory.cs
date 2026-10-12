using System;
using ScreenToGif.Domain.Enums;
using ScreenToGif.Domain.Interfaces;

namespace ScreenToGif.Cloud;

public class CloudFactory
{
    public static IUploader CreateCloud(UploadDestinations service)
    {
        switch (service)
        {
            case UploadDestinations.Imgur:
                return new Imgur();
            case UploadDestinations.Yandex:
                return new YandexDisk();
            case UploadDestinations.CloudflareR2:
                return new CloudflareR2();
        }

        throw new NotImplementedException();
    }
}