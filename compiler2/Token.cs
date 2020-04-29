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

    public Token(string text)
    {
        Id = TokenId.Id;
        Text = text;
        Line = -1;
        Col = -1;
    }

    public override string ToString()
    {
        return $"({Id} `{Regex.Escape(Text)}` @ {Line}:{Col})";
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
    Then,
    Unless,
    Plus,
    Minus,
    Star,
    FSlash,
    Caret,
    Amp,
    Pipe,
    DoubleAmp,
    DoublePipe,
    Exclam,
    LAngle,
    RAngle,
    LAngleEq,
    RAngleEq,
    NotEq,
}