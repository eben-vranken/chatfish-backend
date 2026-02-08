using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using BackEnd.DTOs;
using BackEnd.Services;
using Minio.Exceptions;

namespace BackEnd.Services;

public class PostService
{
    private readonly IMongoCollection<Post> _postCollection;
    private readonly UserService _userService;
    private readonly CommentService _commentService;

    public PostService(
        IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings,
        UserService userService, CommentService commentService)
    {
        var mongoClient = new MongoClient(chatfishDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(chatfishDatabaseSettings.Value.DatabaseName);
        _postCollection = mongoDatabase.GetCollection<Post>(chatfishDatabaseSettings.Value.PostsCollectionName);
        _userService = userService;
        _commentService = commentService;
    }

    private async Task<PostResponse> MapToDto(Post post, string? userId)
    {
        if (post == null) return null;

        User? author = null;
        if (!string.IsNullOrEmpty(post.AuthorId))
        {
            author = await _userService.GetById(post.AuthorId);
        }

        User? requestingUser = null;
        if (!string.IsNullOrEmpty(userId))
        {
            requestingUser = await _userService.GetById(userId);
        }

        bool isOwner = userId != null && post.AuthorId == userId;
        bool isAdmin = requestingUser?.Role?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

        return new PostResponse
        {
            PostId = post.PostId,
            Title = post.Title,
            Content = post.Content,
            AuthorId = post.AuthorId,
            AuthorUsername = author?.Username ?? "unknown",
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt ?? post.CreatedAt,
            ChannelId = post.ChannelId,
            IsArchived = post.IsArchived,
            IsEditable = userId != null && post.AuthorId == userId,
            IsDeletable = isOwner || isAdmin
        };
    }
    private async Task<PostResponse> MapToDto(Post post, User requestingUser)
    {
        if (post == null) return null;

        var author = await _userService.GetById(post.AuthorId);

        bool isOwner = requestingUser != null && post.AuthorId == requestingUser.UserId;
        bool isAdmin = requestingUser != null && requestingUser?.Role?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true;

        return new PostResponse
        {
            PostId = post.PostId,
            Title = post.Title,
            Content = post.Content,
            AuthorId = post.AuthorId,
            AuthorUsername = author?.Username ?? "unknown",
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt ?? post.CreatedAt,
            ChannelId = post.ChannelId,
            IsArchived = post.IsArchived,
            IsEditable = isOwner,
            IsDeletable = isOwner || isAdmin
        };
    }
    private async Task<List<PostResponse>> MapToDtoList(IEnumerable<Post> posts, string userId)
    {
        var list = new List<PostResponse>();
        var requestingUser = await _userService.GetById(userId);
        foreach (var post in posts)
        {
            var dto = await MapToDto(post, requestingUser);
            if (dto != null) list.Add(dto);
        }
        return list;
    }

    public async Task<List<PostResponse>> GetAll()
    {
        var posts = await _postCollection.Find(_ => true).ToListAsync();
        return await MapToDtoList(posts, null);
    }

    public async Task<PostResponse?> Get(string id, string userId)
    {
        var post = await _postCollection.Find(x => x.PostId == id).FirstOrDefaultAsync();
        return await MapToDto(post, userId);
    }

    public async Task<PostResponse> Add(PostCreateRequest postCreateRequest, string userId)
    {
        var newPost = new Post(
            postCreateRequest.Title,
            userId,
            DateTime.UtcNow,
            postCreateRequest.ChannelId,
            postCreateRequest.Content);

        await _postCollection.InsertOneAsync(newPost);
        return await MapToDto(newPost, userId);
    }

    public async Task<PostResponse> Archive(String postId)
    {
        var filter = Builders<Post>.Filter.Eq(x => x.PostId, postId);
        var update = Builders<Post>.Update
            .Set(x => x.IsArchived, true);

        var result = await _postCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Post> { ReturnDocument = ReturnDocument.After }
        );

        if (result == null) return null;

        return await MapToDto(result, String.Empty);
    }

    public async Task<PostResponse?> Update(String authorId, PostUpdateRequest postUpdateRequest)
    {
        var author = await _userService.GetById(authorId);
        if (postUpdateRequest.AuthorId != author.UserId)
        {
            throw new Exception("User does not have permission to update post");
        }
        var filter = Builders<Post>.Filter.Eq(x => x.PostId, postUpdateRequest.PostId);
        var update = Builders<Post>.Update
            .Set(x => x.Title, postUpdateRequest.Title)
            .Set(x => x.Content, postUpdateRequest.Content)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _postCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Post> { ReturnDocument = ReturnDocument.After }
        );

        if (result == null) return null;

        return await MapToDto(result, authorId);
    }


    public async Task<bool> Delete(string postId, string userId)
    {
        var post = Get(postId, userId);
        if (post == null) throw new KeyNotFoundException("Post not found");
        if (post.Result.IsDeletable)
        {
            var result = await _postCollection.DeleteOneAsync(x => x.PostId == postId);
            return result.DeletedCount > 0;
        }

        throw new AuthorizationException("Not authorized to delete post");
    }


    public async Task<List<PostResponse>> GetByChannelId(string channelId, string userId)
    {
        var posts = await _postCollection.Find(x => x.ChannelId == channelId).ToListAsync();
        return await MapToDtoList(posts, userId);
    }

    public async Task<List<PostResponse>> GetPostsByNonArchivedChannelId(string channelId)
    {
        var posts = await _postCollection
            .Find(x => x.ChannelId == channelId && x.IsArchived == false)
            .ToListAsync();

        return await MapToDtoList(posts, null);
    }

    public async Task DeletePostsByChannelId(string channelId)
    {
        var posts = await _postCollection
            .Find(x => x.ChannelId == channelId)
            .ToListAsync();

        foreach (var post in posts)
        {
            if (string.IsNullOrEmpty(post.PostId))
                continue;

            await _commentService.DeleteCommentsByPostId(post.PostId);

            await _postCollection.DeleteOneAsync(x => x.PostId == post.PostId);
        }
    }

}
