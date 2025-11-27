namespace WebApi.Rag.Application;

public record CreateTopicRequest(
    string Title,
    string[] Questions,
    string? Conspect,
    bool GenerateConspect,
    string Lang = "English");

public record CreateSessionRequest(string StudentId, Guid TopicId);

public record SubmitAnswersRequest(List<SubmitItem> Answers);

public record SubmitItem(Guid QuestionId, string Answer);

public record AddDocRequest(string Content, string? Source);