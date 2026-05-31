using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Response;

namespace BackEnd.Util;

public class MinioUtils
{
    public static async Task<string> GetObjectUrlAsync(IMinioClient minioClient, string bucket, string objectName)
    {
        var args = new PresignedGetObjectArgs().
            WithBucket(bucket).
            WithObject(objectName).
            WithExpiry(60 * 60 * 24 * 7);

        return await minioClient.PresignedGetObjectAsync(args);
    }

    public static async Task<ObjectStat> StatObjectAsync(IMinioClient minioClient, string bucket, string objectName)
    {
        var args = new StatObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName);

        return await minioClient.StatObjectAsync(args);
    }

    public static async Task GetObjectAsync(IMinioClient minioClient, string bucket, string objectName, Stream destination)
    {
        var args = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithCallbackStream((stream, cancellationToken) => stream.CopyToAsync(destination, cancellationToken));

        await minioClient.GetObjectAsync(args);
    }
    
    public static async Task<PutObjectResponse> PutObjectAsync(IMinioClient minioClient, string bucket, string objectName, Stream content, string contentType)
    {
        content.Position = 0;
        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        return await minioClient.PutObjectAsync(args);
    }

    public static async Task RemoveObjectAsync(IMinioClient minioClient, string bucket, string objectName)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName);
        
        await minioClient.RemoveObjectAsync(args);
    }
}