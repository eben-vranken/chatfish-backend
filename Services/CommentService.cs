using BackEnd.DTOs;
using BackEnd.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BackEnd.Services;

public class CommentService
{
    private readonly IMongoCollection<Comment> _commentCollection;

    public CommentService(IOptions<ChatfishDatabaseSettings> chatfishDatabaseSettings)
    {
        var mongoClient = new MongoClient(
            chatfishDatabaseSettings.Value.ConnectionString);

        var mongoDatabase = mongoClient.GetDatabase(
            chatfishDatabaseSettings.Value.DatabaseName);

        _commentCollection = mongoDatabase.GetCollection<Comment>(
            chatfishDatabaseSettings.Value.CommentsCollectionName);
    }

    public async Task<List<Comment>> GetByPostId(string postId) =>
        await _commentCollection.Find(x => x.PostId == postId).ToListAsync();

    public async Task<Comment?> Get(string id) =>
        await _commentCollection.Find(x => x.CommentId == id).FirstOrDefaultAsync();

    public async Task<Comment> Add(CommentCreateRequest commentCreateRequest, string authorId)
    {
        var newComment = new Comment(
            commentCreateRequest.Content,
            authorId,
            commentCreateRequest.PostId,
            DateTime.UtcNow);

        await _commentCollection.InsertOneAsync(newComment);
        return newComment;
    }

    public async Task Delete(string id) =>
        await _commentCollection.DeleteOneAsync(x => x.CommentId == id);

    public async Task DeleteCommentsByPostId(string postId)
    {
        var comments = await GetByPostId(postId);

        foreach (var comment in comments)
        {
            if (!string.IsNullOrEmpty(comment.CommentId))
            {
                await Delete(comment.CommentId);
            }
        }
    }
    
    public async Task Edit(Comment comment)
    {
        await _commentCollection.ReplaceOneAsync(x => x.CommentId == comment.CommentId, comment);
    }
    
}
