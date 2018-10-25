// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.Configuration.Json
{
    internal class JsonConfigurationFileParser
    {
        private JsonConfigurationFileParser() { }

        private readonly IDictionary<string, string> _data = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _context = new Stack<string>();
        private string _currentPath;

        private JsonTextReader _reader;

        public static IDictionary<string, string> Parse(Stream input) => new JsonConfigurationFileParser().ParseStream(input);

        /// <summary>
        /// Parse a stream to a Json string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string ParseToString(Stream input)
        {
           return new JsonConfigurationFileParser().ParseStreamToString(input);
        }

        private IDictionary<string, string> ParseStream(Stream input)
        {
            _data.Clear();

            //Parse a stream to a Json Object
            var jsonConfig = ParseStreamToJsonObject(input);
            
            VisitJObject(jsonConfig);

            return _data;
        }

        /// <summary>
        /// Parse a stream to a Json Object
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private JObject ParseStreamToJsonObject(Stream input)
        {
            //Read stream to json textreader
            _reader = new JsonTextReader(new StreamReader(input))
            {
                DateParseHandling = DateParseHandling.None
            };

            //load JObject
            var jsonConfig = JObject.Load(_reader);

            //Set stream position to the start
            input.Position = 0;

            //Return JObject
            return jsonConfig;
        }

        /// <summary>
        /// Parse a stream to a generic object
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string ParseStreamToString(Stream input)
        {
            //Read stream to json textreader
            var jsonConfig = ParseStreamToJsonObject(input);

            //Convert to generic
            return jsonConfig.ToString(Formatting.None);
        }

        private void VisitJObject(JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                EnterContext(property.Name);
                VisitProperty(property);
                ExitContext();
            }
        }

        private void VisitProperty(JProperty property)
        {
            VisitToken(property.Value);
        }

        private void VisitToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    VisitJObject(token.Value<JObject>());
                    break;

                case JTokenType.Array:
                    VisitArray(token.Value<JArray>());
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Bytes:
                case JTokenType.Raw:
                case JTokenType.Null:
                    VisitPrimitive(token.Value<JValue>());
                    break;

                default:
                    throw new FormatException(Resources.FormatError_UnsupportedJSONToken(
                        _reader.TokenType,
                        _reader.Path,
                        _reader.LineNumber,
                        _reader.LinePosition));
            }
        }

        private void VisitArray(JArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                EnterContext(index.ToString());
                VisitToken(array[index]);
                ExitContext();
            }
        }

        private void VisitPrimitive(JValue data)
        {
            var key = _currentPath;

            if (_data.ContainsKey(key))
            {
                throw new FormatException(Resources.FormatError_KeyIsDuplicated(key));
            }
            _data[key] = data.ToString(CultureInfo.InvariantCulture);
        }

        private void EnterContext(string context)
        {
            _context.Push(context);
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }

        private void ExitContext()
        {
            _context.Pop();
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }
    }
}
