﻿// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Bot.Builder.Dialogs.Adaptive.Recognizers
{
    /// <summary>
    /// Defines map of languages -> recognizer
    /// </summary>
    public class MultiLanguageRecognizer : IRecognizer
    {

        /// <summary>
        /// Policy for languages fallback 
        /// </summary>
        [JsonProperty("languagePolicy")]
        public ILanguagePolicy LanguagePolicy { get; set; } = new LanguagePolicy();

        /// <summary>
        /// Map of languages -> IRecognizer
        /// </summary>
        [JsonProperty("recognizers")]
        public IDictionary<string, IRecognizer> Recognizers { get; set; } = new Dictionary<string, IRecognizer>();

        public MultiLanguageRecognizer()
        {

        }

        public Task<RecognizerResult> RecognizeAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (!LanguagePolicy.TryGetValue(turnContext.Activity.Locale ?? string.Empty, out string[] policy))
                policy = new string[] { string.Empty };

            foreach (var option in policy)
            {
                if (this.Recognizers.TryGetValue(option, out IRecognizer recognizer))
                {
                    return recognizer.RecognizeAsync(turnContext, cancellationToken);
                }
            }
            // nothing recognized
            return Task.FromResult(new RecognizerResult() { });
        }

        public Task<T> RecognizeAsync<T>(ITurnContext turnContext, CancellationToken cancellationToken) where T : IRecognizerConvert, new()
        {
            if (!LanguagePolicy.TryGetValue(turnContext.Activity.Locale ?? string.Empty, out string[] policy))
                policy = new string[] { string.Empty };

            foreach (var option in policy)
            {
                if (this.Recognizers.TryGetValue(option, out IRecognizer recognizer))
                {
                    return recognizer.RecognizeAsync<T>(turnContext, cancellationToken);
                }
            }
            // nothing recognized
            return Task.FromResult(default(T));
        }
    }
}
