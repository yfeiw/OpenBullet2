﻿namespace OpenBullet2.Web.Dtos.Config;

/// <summary>
/// DTO that contains the readme of the config.
/// </summary>
public class ConfigReadmeDto
{
    /// <summary>
    /// The markdown text of the readme.
    /// </summary>
    public string MarkdownText { get; set; } = default!;
}
