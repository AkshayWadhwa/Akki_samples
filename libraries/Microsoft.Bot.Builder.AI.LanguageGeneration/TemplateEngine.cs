﻿using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Newtonsoft.Json;
using System;

namespace Microsoft.Bot.Builder.AI.LanguageGeneration
{
    /// <summary>
    /// The template engine that loads .lg file and eval template based on memory/scope
    /// </summary>
    public class TemplateEngine
    {
        /// <summary>
        /// Parsed LG templates
        /// </summary>
        public List<LGTemplate> Templates = new List<LGTemplate>();

        /// <summary>
        /// Return an empty engine, you can use AddFiles to add files to it, 
        /// or you can just use this empty engine to evaluate inline template
        /// </summary>
        public TemplateEngine()
        {
        }

        /// <summary>
        /// Add a file to a template engine
        /// </summary>
        /// <param name="filePath">the path to the file</param>
        /// <returns>template engine with the parsed content of this file</returns>
        public TemplateEngine AddFile(string filePath)
        {
            var text = File.ReadAllText(filePath);
            Templates.AddRange(ToTemplates(Parse(text), filePath));

            RunStaticCheck();
            return this;
        }

        /// <summary>
        /// Add text as lg file content to template engine
        /// </summary>
        /// <param name="text">the text content contains lg templates</param>
        /// <returns>template engine with the parsed content</returns>
        public TemplateEngine AddText(string text)
        {
            Templates.AddRange(ToTemplates(Parse(text), "text"));

            RunStaticCheck();
            return this;
        }

        /// <summary>
        /// Parse text as a LG file using antlr
        /// </summary>
        /// <param name="text">text to parse</param>
        /// <returns>ParseTree of the LG file</returns>
        private LGFileParser.FileContext Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var input = new AntlrInputStream(text);
            var lexer = new LGFileLexer(input);
            var tokens = new CommonTokenStream(lexer);
            var parser = new LGFileParser(tokens);
            parser.RemoveErrorListeners();
            var listener = new ErrorListener();

            parser.AddErrorListener(listener);
            parser.BuildParseTree = true;

            return parser.file();
        }

        /// <summary>
        /// Convert a file parse tree to a list of LG templates
        /// </summary>
        /// <returns></returns>
        private List<LGTemplate> ToTemplates(LGFileParser.FileContext file, string source = "")
        {
            if (file == null)
            {
                return new List<LGTemplate>();
            }

            var templates = file.paragraph().Select(x => x.templateDefinition()).Where(x => x != null);
            return templates.Select(t => new LGTemplate(t, source)).ToList();
        }



        public void RunStaticCheck(List<LGTemplate> templates = null)
        {
            var teamplatesToCheck = templates ?? Templates;
            var checker = new StaticChecker(teamplatesToCheck);
            var report = checker.Check();

            var errors = report.Where(u => u.Type == ReportEntryType.ERROR).ToList();
            if (errors.Count != 0)
            {
                throw new Exception(string.Join("\n", errors));
            }
        }
        
        public string EvaluateTemplate(string templateName, object scope, IGetMethod methodBinder = null)
        {

            var evaluator = new Evaluator(Templates, methodBinder);
            return evaluator.EvaluateTemplate(templateName, scope);
        }

        public List<string> AnalyzeTemplate(string templateName)
        {
            var analyzer = new Analyzer(Templates);
            return analyzer.AnalyzeTemplate(templateName);
        }


        /// <summary>
        /// Use to evaluate an inline template str
        /// </summary>
        /// <param name="inlineStr"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public string Evaluate(string inlineStr, object scope, IGetMethod methodBinder = null)
        {
            // wrap inline string with "# name and -" to align the evaluation process
            var fakeTemplateId = "__temp__";
            var wrappedStr = $"# {fakeTemplateId} \r\n - {inlineStr}";
      
            var parsedTemplates = ToTemplates(Parse(wrappedStr), "inline");
            // merge the existing templates and this new template as a whole for evaluation
            var mergedTemplates = Templates.Concat(parsedTemplates).ToList();

            RunStaticCheck(mergedTemplates);

            var evaluator = new Evaluator(mergedTemplates, methodBinder);
            return evaluator.EvaluateTemplate(fakeTemplateId, scope);

        }


        /// <summary>
        /// Create a template engine from a file, equivalent to 
        ///    new TemplateEngine.AddFile(filePath)
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static TemplateEngine FromFile(string filePath)
        {
            return new TemplateEngine().AddFile(filePath);
        }

        /// <summary>
        /// Create a template engine from text, equivalent to 
        ///    new TemplateEngine.AddText(text)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static TemplateEngine FromText(string text)
        {
            return new TemplateEngine().AddText(text);
        }
    }
}
