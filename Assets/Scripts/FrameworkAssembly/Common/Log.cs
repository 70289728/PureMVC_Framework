using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;

public static class Log
{
    private const string NormalColor  = "<color=#00FF00>";
    private const string WarningColor = "<color=#FFFF00>";
    private const string ErrorColor   = "<color=#FF00FF>";
    private const string ColorEnd     = "</color>";

    private static string BuildMessage(string color, object message, object tag)
    {
        if (tag == null) return message.ToString();
        // Use local StringBuilder to avoid shared-state issues
        var sb = new StringBuilder();
        sb.Append(color);
        sb.Append(" [");
        sb.Append(tag.ToString());
        sb.Append("] ");
        sb.Append(ColorEnd);
        sb.Append(message.ToString());
        return sb.ToString();
    }

    public static void d(object message, object tag = null)
    {
        UnityEngine.Debug.Log(BuildMessage(NormalColor, message, tag));
    }

    public static void w(object message, object tag = null)
    {
        UnityEngine.Debug.LogWarning(BuildMessage(WarningColor, message, tag));
    }

    public static void e(object message, object tag = null)
    {
        UnityEngine.Debug.LogError(BuildMessage(ErrorColor, message, tag));
    }
}
