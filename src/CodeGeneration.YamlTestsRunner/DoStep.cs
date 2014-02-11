﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using YamlDotNet.Dynamic;

namespace CodeGeneration.YamlTestsRunner
{
	public class DoStep : ITestStep
	{

		public string Type { get { return "do"; } }
		public string Call { get; set; }
		public string Catch { get; set; }

		private string _rawElasticSearchCall = null;
		public string RawElasticSearchCall
		{
			get
			{
				if (!_rawElasticSearchCall.IsNullOrEmpty())
					return _rawElasticSearchCall;
				_rawElasticSearchCall = FindBestRawElasticSearchMatch();
				return _rawElasticSearchCall;
			}
		}
		public object Body { get; set; }
		public NameValueCollection QueryString { get; set; }

		private string FindBestRawElasticSearchMatch()
		{
			if (this.Call == "create")
			{
				this.Call = "index";
				if (this.QueryString == null)
					this.QueryString = new NameValueCollection();
				this.QueryString.Add("op_type", "create");
			}

			var re = "^" + this.Call.ToPascalCase() + @"(Get|Put|Head|Post|Delete)?\(";
			var calls = YamlTestsGenerator.RawElasticCalls
				.Where(c => Regex.IsMatch(c, re))
				.OrderByDescending(QueryStringCount)
				.ThenByDescending(MethodPreference)
				.ToList();

			var call = calls.FirstOrDefault();
			if (call == null)
			{
				//todo figure out what to do here.
				return string.Empty;
			}
			return this.GenerateCall(call);
		}

		public string SerializeBody()
		{
			if (this.Body == null)
				return null;
			var body = "_body = ";
			var s = this.Body as string;
			if (s != null)
			{
				body += string.Format("@\"{0}\";", EscapeQuotes(s));
				return body;
			}
			var ss = this.Body as IEnumerable<string>;
			if (ss != null)
			{
				body += string.Format("@\"{0}\";", string.Join("\n", ss.Select(EscapeQuotes)));
				return body;
			}
			
			var os = this.Body as IEnumerable<object>;
			if (os != null)
			{

				body += string.Format("@\"{0}\";", string.Join("\n", os
					.Select(oss=>EscapeQuotes(JsonConvert.SerializeObject(oss, Formatting.None)))));
				return body;
			}
			body += this.SerializeToAnonymousObject(this.Body) + ";\n";
			return body;
		}

		private string SerializeToAnonymousObject(object o)
		{
			var serializer = new JsonSerializer() { Formatting = Formatting.Indented };
			var stringWriter = new StringWriter();
			var writer = new JsonTextWriter(stringWriter);
			writer.QuoteName = false;
			serializer.Serialize(writer, o);
			writer.Close();
			//anonymousify the json
			var anon = stringWriter.ToString().Replace("{", "new {").Replace("]", "}").Replace("[", "new [] {").Replace(":", "=");
			//match indentation of the view	
			anon = Regex.Replace(anon, @"^(\s+)?", (m) =>
			{
				if (m.Index == 0)
					return m.Value;
				return "\t\t\t\t" + m.Value.Replace("  ", "\t");
			}, RegexOptions.Multiline);
			//escape c# keywords in the anon object
			anon = anon.Replace("default=", "@default=").Replace("params=", "@params=");
			//docs contain different types of anon objects, quick fix by making them a dynamic[]
			anon = anon.Replace("docs= new []", "docs= new dynamic[]");
			//fix empty untyped arrays, default to string
			anon = anon.Replace("new [] {}", "new string[] {}");
			//quick fixes for settings: index.* and discovery.zen.*
			//needs some recursive regex love perhaps in the future
			anon = Regex.Replace(anon, @"^(\s+)(index)\.([^\.]+)=([^\r\n]+)", "$1$2= new { $3=$4 }", RegexOptions.Multiline);
			anon = Regex.Replace(anon, @"^(\s+)(discovery)\.([^\.]+)\.([^\.]+)=(.+)$", "$1$2= new { $3= new { $4= $5 } }", RegexOptions.Multiline);
			return anon;
		}

		private string EscapeQuotes(string s)
		{
			return s.Replace("\"", "\"\"");
		}

		private string GenerateCall(string call)
		{
			var s = "_status = this._client.";
			var csharpMethod = call.Split('(').First();
			s += csharpMethod + "(";
			var csharpArguments = CsharpArguments(call);
			var args = csharpArguments
				.Select(this.GetQueryStringValue)
				.ToList();
			if (this.Body != null)
			{
				args.Add("_body");
			}
			else if (call.Contains("object body,"))
			{
				args.Add("null");
			}
			var queryStringKeys = this.CsharpArguments(call, inverse: true);
			if (queryStringKeys.Any())
			{
				var nv = "nv=>nv\r\n";
				nv += queryStringKeys.Aggregate("",
					(current, k) => current + string.Format("\t\t\t\t\t.Add(\"{0}\",{1})\r\n", k, this.GetQueryStringValue(k)));
				nv += "\t\t\t\t";
				args.Add(nv);
			}

			s += string.Join(", ", args);
			s += ");";
			s += "\r\n\t\t\t\t_response = _status.Deserialize<dynamic>();";
			return s;
		}

		private string GetQueryStringValue(string key)
		{
			var value = this.QueryString[key];
			if (value.StartsWith("$"))
				return value.Replace("$", "");
			return "\"" + value + "\"";
		}

		private IEnumerable<string> CsharpArguments(string call, bool inverse = false)
		{
			var csharpArguments = this.QueryString.AllKeys
				.Select(k => new {Key = k, Index = call.IndexOf(k + ",", System.StringComparison.Ordinal)})
				.Where(ki => inverse ? ki.Index < 0 : ki.Index >= 0)
				.OrderBy(ki => ki.Index)
				.Select(ki => ki.Key);
			return csharpArguments.ToList();
		}

		private int QueryStringCount(string method)
		{
			return QueryString.AllKeys.Count(k => method.Contains(k + ","));
		}
		private int MethodPreference(string method)
		{
			var postBoost = this.Body != null ? 10 : 0;
			var getBoost = this.Body == null ? 10 : 0;

			if (method.Contains("Post(")) return 5 + postBoost;
			if (method.Contains("Put(")) return 4 + postBoost;
			if (method.Contains("Get(")) return 3 + getBoost;
			if (method.Contains("Head(")) return 2 + postBoost;
			return 0;
		}
	}
}