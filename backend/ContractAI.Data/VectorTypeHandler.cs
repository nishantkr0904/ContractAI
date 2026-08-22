using System.Data;
using Dapper;
using Pgvector;

namespace ContractAI.Data;

// Dapper binds parameters through its own type map, which has no entry for
// Pgvector.Vector, so a vector parameter throws NotSupportedException before the
// command ever reaches Npgsql — the data source's UseVector() mapping only covers
// the ADO layer. Reads need no conversion: that same mapping already hands back a
// Vector, so Parse is a cast.
internal sealed class VectorTypeHandler : SqlMapper.TypeHandler<Vector>
{
    public override void SetValue(IDbDataParameter parameter, Vector? value)
    {
        // No DbType is set: Npgsql infers `vector` from the CLR type via the data
        // source mapping, and Dapper skips its own DbType assignment whenever a
        // handler is registered.
        parameter.Value = value;
    }

    public override Vector Parse(object value) => (Vector)value;
}
