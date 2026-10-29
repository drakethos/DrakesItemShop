using System;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakesItemShop.Trade;

/// <summary>"Bjorn wants to trade with you": Accept or Decline, gone after 30 seconds.</summary>
internal static class TradePrompt
{
    private static UkWindow? _window;

    /// <summary>Close the prompt without answering (the asker cancelled).</summary>
    public static void Dismiss()
    {
        _window?.CloseSilent();
        _window = null;
    }

    public static void Show(string who, int seconds, Action accept, Action decline)
    {
        _window?.CloseSilent();
        var window = new UkWindow("drakes_trade_request", 400, UkWindow.DialogLayer, dim: false, draggable: false);
        window.BlocksInput = false;
        window.DockAside();
        window.SetCardOpacity(TradeSettings.PromptOpacity);
        _window = window;
        window.SetHeader("Trade request");

        var text = UkControls.Text($"{who} wants to trade with you.", 18, UkTheme.Text);
        var countdown = UkControls.Text($"Expires in {seconds} s", 13, UkTheme.TextMuted, wrap: false);
        countdown.style.marginTop = 6;
        window.Body.Add(text);
        window.Body.Add(countdown);

        var answered = false;
        void Answer(bool yes)
        {
            if (answered)
                return;
            answered = true;
            window.CloseSilent();
            if (yes)
                accept();
            else
                decline();
        }

        var left = seconds;
        var timer = window.Body.schedule.Execute(() =>
        {
            left--;
            countdown.text = $"Expires in {left} s";
            if (left <= 0)
                Answer(false);
        }).Every(1000);

        window.Footer.Add(UkControls.MakeButton("Decline", () => Answer(false), UkButtonKind.Secondary));
        window.Footer.Add(UkControls.Spacer());
        window.Footer.Add(UkControls.MakeButton("Accept", () => Answer(true), UkButtonKind.Primary, 130));
        window.ShowFooter(true);
        window.Closed = () =>
        {
            timer.Pause();
            Answer(false);
        };
        window.Open();
    }
}
