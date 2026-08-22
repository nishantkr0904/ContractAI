namespace ContractAI.Core.Interfaces;

// Turns clause or query text into a dense vector for semantic search. Kept in Core
// as a provider-agnostic capability (same reasoning as IBlobStorageService): the
// analysis pipeline and search depend on "embed this text", not on Google.
// ContractAI.Services binds it to the Gemini embeddings API.
public interface IEmbeddingService
{
    // Returns a unit-normalized vector whose length is the configured embedding
    // dimension (1536, to match the contract_clauses.embedding column). taskType
    // lets the provider tune the vector for its role: a stored clause embeds as a
    // document, an incoming search string as a query.
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingTaskType taskType = EmbeddingTaskType.RetrievalDocument,
        CancellationToken cancellationToken = default);
}

// Retrieval embeddings are asymmetric: the model maps a document and the query that
// should find it into the shared space differently. Passing the right role at embed
// time is what makes cosine distance between the two meaningful.
public enum EmbeddingTaskType
{
    RetrievalDocument,
    RetrievalQuery,
}
