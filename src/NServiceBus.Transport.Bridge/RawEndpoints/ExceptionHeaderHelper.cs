﻿namespace NServiceBus.Raw
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    static class ExceptionHeaderHelper
    {
        public static void SetExceptionHeaders(Dictionary<string, string> headers, Exception e)
        {
            headers["NServiceBus.Transport.Bridge.ExceptionInfo.ExceptionType"] = e.GetType().FullName;

            if (e.InnerException != null)
            {
                headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = e.InnerException.GetType().FullName;
            }

            headers["NServiceBus.Transport.Bridge.ExceptionInfo.HelpLink"] = e.HelpLink;
            headers["NServiceBus.Transport.Bridge.ExceptionInfo.Message"] = e.GetMessage().Truncate(16384);
            headers["NServiceBus.Transport.Bridge.ExceptionInfo.Source"] = e.Source;
            headers["NServiceBus.Transport.Bridge.ExceptionInfo.StackTrace"] = e.ToString();
            headers["NServiceBus.Transport.Bridge.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (e.Data == null)
            // ReSharper disable HeuristicUnreachableCode
            {
                return;
            }
            // ReSharper restore HeuristicUnreachableCode

            foreach (DictionaryEntry entry in e.Data)
            {
                if (entry.Value == null)
                {
                    continue;
                }
                headers["NServiceBus.Transport.Bridge.ExceptionInfo.Data." + entry.Key] = entry.Value.ToString();
            }
        }

        static string GetMessage(this Exception exception)
        {
            try
            {
                return exception.Message;
            }
            catch (Exception)
            {
                return $"Could not read Message from exception type '{exception.GetType()}'.";
            }
        }

        static string Truncate(this string value, int maxLength) =>
            string.IsNullOrEmpty(value)
                ? value
                : (value.Length <= maxLength
                    ? value
                    : value.Substring(0, maxLength));
    }
}