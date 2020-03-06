#nullable enable
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public struct Token
{
    public string Text;
    public TokenId Id;

    public Token(TokenId id)
    {
        Id = id;
        Text = string.Empty;
    }

    public Token(TokenId id, string text)
    {
        Id = id;
        Text = text;
    }

    public override string ToString()
    {
        return $"({Id},`{Regex.Escape(Text)}`)";
    }
}

public enum TokenId
{
    Eof,
    Id,
    Fn,
    LParen,
    RParen,
    Begin,
    End,
}