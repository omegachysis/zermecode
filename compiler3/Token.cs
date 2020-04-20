#nullable enable

using System;
using System.Text.RegularExpressions;

public struct Token
{
    public TokenId Id;
    public string Text;
    public int Line;
    public int Col;

    /// <summary>
    /// Create a token for a non-identifier.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="line"></param>
    /// <param name="col"></param>
    public Token(TokenId id, int line, int col)
    {
        if (id == TokenId.Id)
            throw new ArgumentException(nameof(id));

        Id = id;
        Text = string.Empty;
        Line = line;
        Col = col;
    }

    /// <summary>
    /// Create a new token for an identifier.
    /// </summary>
    /// <param name="idText"></param>
    public Token(string idText, int line, int col)
    {
        Id = TokenId.Id;
        Text = idText;
        Line = line;
        Col = col;
    }

    /// <summary>
    /// Create a new token for a compiler-generated identifier.
    /// </summary>
    /// <param name="idText"></param>
    public Token(string idText)
    {
        Id = TokenId.Id;
        Text = idText;
        Line = 0;
        Col = 0;
    }

    public bool IsCompilerGenerated => Line == 0;

    public override string ToString()
    {
        var locText = $"{Line}:{Col}";
        if (Text == string.Empty)
            return $"<{Id}, {locText}>";
        else
            return $"<`{Regex.Escape(Text)}`, {locText}>";
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