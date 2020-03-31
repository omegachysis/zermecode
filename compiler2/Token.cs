#nullable enable
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public struct Token
{
    public string Text;
    public TokenId Id;
    public int Line;
    public int Col;

    public Token(TokenId id, int line, int col)
    {
        Id = id;
        Text = string.Empty;
        Line = line;
        Col = col;
    }

    public Token(TokenId id, string text, int line, int col)
    {
        Id = id;
        Text = text;
        Line = line;
        Col = col;
    }

    public override string ToString()
    {
        return $"({Id}    `{Text}`    {Line}:{Col})";
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
    RArrow,
    Comma,
    Semi,
    Num,
    Eq,
    Op,
    Str,
    Bool,
    Return,
    Type,
    Dot,
    Assign,
    Colon,
    If,
    Else,
    Let,
}