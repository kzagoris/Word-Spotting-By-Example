using System;
using System.Text;
using System.Threading;

/// <summary>
/// An ASCII progress bar
/// </summary>
public class ProgressBar : IDisposable, IProgress<int>
{
    private const int BlockCount = 10;
    private readonly TimeSpan AnimationInterval = TimeSpan.FromSeconds(1.0 / 8);
    private const string Animation = @"|/-\";

    private readonly Timer Timer;

    private double CurrentProgress;
    private string CurrentText = string.Empty;
    private bool Disposed;
    private int AnimationIndex;

    public ProgressBar()
    {
        Timer = new Timer(TimerHandler);

        // A progress bar is only for temporary display in a console window.
        // If the console output is redirected to a file, draw nothing.
        // Otherwise, we'll end up with a lot of garbage in the target file.
        if (!Console.IsOutputRedirected)
        {
            ResetTimer();
        }
    }

    public void Report(int value)
    {
        // Make sure value is in [0..100] range
        value = Math.Max(0, Math.Min(100, value));
        Interlocked.Exchange(ref CurrentProgress, value);
    }

    public void Increase(double value)
    {
        Add(ref CurrentProgress, value);
    }

    private void TimerHandler(object state)
    {
        lock (Timer)
        {
            if (Disposed) return;

            int progressBlockCount = (int)(CurrentProgress / 100 * BlockCount);
            string text = string.Format("[{0}{1}] {2,3}% {3}",
                new string('#', progressBlockCount), new string('-', BlockCount - progressBlockCount),
                CurrentProgress.ToString("0.00"),
                Animation[AnimationIndex++ % Animation.Length]);
            UpdateText(text);

            ResetTimer();
        }
    }

    private void UpdateText(string text)
    {
        // Get length of common portion
        int commonPrefixLength = 0;
        int commonLength = Math.Min(CurrentText.Length, text.Length);
        while (commonPrefixLength < commonLength && text[commonPrefixLength] == CurrentText[commonPrefixLength])
        {
            commonPrefixLength++;
        }

        // Backtrack to the first differing character
        StringBuilder outputBuilder = new StringBuilder();
        outputBuilder.Append('\b', CurrentText.Length - commonPrefixLength);

        // Output new suffix
        outputBuilder.Append(text.Substring(commonPrefixLength));

        // If the new text is shorter than the old one: delete overlapping characters
        int overlapCount = CurrentText.Length - text.Length;
        if (overlapCount > 0)
        {
            outputBuilder.Append(' ', overlapCount);
            outputBuilder.Append('\b', overlapCount);
        }

        Console.Write(outputBuilder);
        CurrentText = text;
    }

    private void ResetTimer()
    {
        Timer.Change(AnimationInterval, TimeSpan.FromMilliseconds(-1));
    }

    public void Dispose()
    {
        lock (Timer)
        {
            Disposed = true;
            UpdateText(string.Empty);
        }
    }
    
    // <summary>
    ///   Adds two 32-bit floating point values and replaces the first
    ///   double value with their sum, as an atomic operation.
    /// </summary>
    /// 
    /// <param name="location1">The first variable to be added.</param>
    /// <param name="value">The second variable to be added.</param>
    /// 
    /// <returns>The updated value of the first variable.</returns>
    /// 
    public static double Add(ref double location1, double value)
    {
        double newCurrentValue = 0;
        while (true)
        {
            double currentValue = newCurrentValue;
            double newValue = currentValue + value;
            newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
            if (Math.Abs(newCurrentValue - currentValue) < double.Epsilon)
                return newValue;
        }
    }

}
