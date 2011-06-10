﻿using System;
using System.Collections.Generic;
using System.IO;
using Google.ProtocolBuffers.Descriptors;

namespace Google.ProtocolBuffers.Serialization
{
    /// <summary>
    /// JsonFormatWriter is a .NET 2.0 friendly json formatter for proto buffer messages.  For .NET 3.5
    /// you may also use the XmlFormatWriter with an XmlWriter created by the
    /// <see cref="System.Runtime.Serialization.Json.JsonReaderWriterFactory">JsonReaderWriterFactory</see>.
    /// </summary>
    public class JsonFormatWriter : AbstractTextWriter
    {
        private readonly char[] _buffer;
        private readonly TextWriter _output;
        private readonly List<int> _counter;
        private bool _isArray;
        int _bufferPos;
        /// <summary>
        /// Constructs a JsonFormatWriter to output to a new instance of a StringWriter, use
        /// the ToString() member to extract the final Json on completion.
        /// </summary>
        public JsonFormatWriter() : this(new StringWriter()) { }
        /// <summary>
        /// Constructs a JsonFormatWriter to output to the given text writer
        /// </summary>
        public JsonFormatWriter(TextWriter output)
        {
            _buffer = new char[4096];
            _bufferPos = 0;
            _output = output;
            _counter = new List<int>();
            _counter.Add(0);
        }


        private void WriteToOutput(string format, params object[] args)
        { WriteToOutput(String.Format(format, args)); }

        private void WriteToOutput(string text)
        { WriteToOutput(text.ToCharArray(), 0, text.Length); }

        private void WriteToOutput(char[] chars, int offset, int len)
        {
            if (_bufferPos + len >= _buffer.Length)
                Flush();
            if (len < _buffer.Length)
            {
                if (len <= 12)
                {
                    int stop = offset + len;
                    for (int i = offset; i < stop; i++)
                        _buffer[_bufferPos++] = chars[i];
                }
                else
                {
                    Buffer.BlockCopy(chars, offset << 1, _buffer, _bufferPos << 1, len << 1);
                    _bufferPos += len;
                }
            }
            else
                _output.Write(chars, offset, len);
        }

        private void WriteToOutput(char ch)
        {
            if (_bufferPos >= _buffer.Length)
                Flush();
            _buffer[_bufferPos++] = ch;
        }

        public override void Flush()
        {
            if (_bufferPos > 0)
            {
                _output.Write(_buffer, 0, _bufferPos);
                _bufferPos = 0;
            }
            base.Flush();
        }

        /// <summary>
        /// Returns the output of TextWriter.ToString() where TextWriter is the ctor argument.
        /// </summary>
        public override string ToString()
        { Flush(); return _output.ToString(); }

        /// <summary> Sets the output formatting to use Environment.NewLine with 4-character indentions </summary>
        public JsonFormatWriter Formatted()
        {
            NewLine = Environment.NewLine;
            Indent = "    ";
            Whitespace = " ";
            return this;
        }

        /// <summary> Gets or sets the characters to use for the new-line, default = empty </summary>
        public string NewLine { get; set; }
        /// <summary> Gets or sets the text to use for indenting, default = empty </summary>
        public string Indent { get; set; }
        /// <summary> Gets or sets the whitespace to use to separate the text, default = empty </summary>
        public string Whitespace { get; set; }

        private void Seperator()
        {
            if (_counter.Count == 0)
                throw new InvalidOperationException("Missmatched open/close in Json writer.");

            int index = _counter.Count - 1;
            if (_counter[index] > 0)
                WriteToOutput(',');

            WriteLine(String.Empty);
            _counter[index] = _counter[index] + 1;
        }

        private void WriteLine(string content)
        {
            if (!String.IsNullOrEmpty(NewLine))
            {
                WriteToOutput(NewLine);
                for (int i = 1; i < _counter.Count; i++)
                    WriteToOutput(Indent);
            }
            else if(!String.IsNullOrEmpty(Whitespace))
                WriteToOutput(Whitespace);

            WriteToOutput(content);
        }

        private void WriteName(string field)
        {
            Seperator();
            if (!String.IsNullOrEmpty(field))
            {
                WriteToOutput('"');
                WriteToOutput(field);
                WriteToOutput('"');
                WriteToOutput(':');
                if (!String.IsNullOrEmpty(Whitespace))
                    WriteToOutput(Whitespace);
            }
        }

        private void EncodeText(string value)
        {
            char[] text = value.ToCharArray();
            int len = text.Length;
            int pos = 0;

            while (pos < len)
            {
                int next = pos;
                while (next < len && text[next] >= 32 && text[next] < 127 && text[next] != '\\' && text[next] != '/' && text[next] != '"')
                    next++;
                WriteToOutput(text, pos, next - pos);
                if (next < len)
                {
                    switch (text[next])
                    {
                        case '"': WriteToOutput(@"\"""); break;
                        case '\\': WriteToOutput(@"\\"); break;
                        //odd at best to escape '/', most Json implementations don't, but it is defined in the rfc-4627
                        case '/': WriteToOutput(@"\/"); break; 
                        case '\b': WriteToOutput(@"\b"); break;
                        case '\f': WriteToOutput(@"\f"); break;
                        case '\n': WriteToOutput(@"\n"); break;
                        case '\r': WriteToOutput(@"\r"); break;
                        case '\t': WriteToOutput(@"\t"); break;
                        default: WriteToOutput(@"\u{0:x4}", (int)text[next]); break;
                    }
                    next++;
                }
                pos = next;
            }
        }

        /// <summary>
        /// Writes a String value
        /// </summary>
        protected override void WriteAsText(string field, string textValue, object typedValue)
        {
            WriteName(field);
            if(typedValue is bool || typedValue is int || typedValue is uint || typedValue is long || typedValue is ulong || typedValue is double || typedValue is float)
                WriteToOutput(textValue);
            else
            {
                WriteToOutput('"');
                if (typedValue is string)
                    EncodeText(textValue);
                else
                    WriteToOutput(textValue);
                WriteToOutput('"');
            }
        }

        /// <summary>
        /// Writes a Double value
        /// </summary>
        protected override void Write(string field, double value)
        {
            if (double.IsNaN(value) || double.IsNegativeInfinity(value) || double.IsPositiveInfinity(value))
                throw new InvalidOperationException("This format does not support NaN, Infinity, or -Infinity");
            base.Write(field, value);
        }

        /// <summary>
        /// Writes a Single value
        /// </summary>
        protected override void Write(string field, float value)
        {
            if (float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
                throw new InvalidOperationException("This format does not support NaN, Infinity, or -Infinity");
            base.Write(field, value);
        }

        // Treat enum as string
        protected override void WriteEnum(string field, int number, string name)
        {
            Write(field, name);
        }

        /// <summary>
        /// Writes an array of field values
        /// </summary>
        protected override void WriteArray(FieldType type, string field, System.Collections.IEnumerable items)
        {
            System.Collections.IEnumerator enumerator = items.GetEnumerator();
            try { if (!enumerator.MoveNext()) return; }
            finally { if (enumerator is IDisposable) ((IDisposable)enumerator).Dispose(); }

            WriteName(field);
            WriteToOutput("[");
            _counter.Add(0);

            base.WriteArray(type, String.Empty, items);

            _counter.RemoveAt(_counter.Count - 1);
            WriteLine("]");
        }

        /// <summary>
        /// Writes a message
        /// </summary>
        protected override void WriteMessageOrGroup(string field, IMessageLite message)
        {
            WriteName(field);
            WriteMessage(message);
        }

        /// <summary>
        /// Writes the message to the the formatted stream.
        /// </summary>
        public override void WriteMessage(IMessageLite message)
        {
            if (_isArray) Seperator();
            WriteToOutput("{");
            _counter.Add(0);
            message.WriteTo(this);
            _counter.RemoveAt(_counter.Count - 1);
            WriteLine("}");
            Flush();
        }

        /// <summary>
        /// Writes a message
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public override void WriteMessage(string field, IMessageLite message)
        {
            WriteMessage(message);
        }

        /// <summary>
        /// Used in streaming arrays of objects to the writer
        /// </summary>
        /// <example>
        /// <code>
        /// using(writer.StartArray())
        ///     foreach(IMessageLite m in messages)
        ///         writer.WriteMessage(m);
        /// </code>
        /// </example>
        public sealed class JsonArray : IDisposable 
        {
            JsonFormatWriter _writer;
            internal JsonArray(JsonFormatWriter writer)
            {
                _writer = writer;
                _writer.WriteToOutput("[");
                _writer._counter.Add(0);
            }

            /// <summary>
            /// Causes the end of the array character to be written.
            /// </summary>
            void EndArray() 
            {
                if (_writer != null)
                {
                    _writer._counter.RemoveAt(_writer._counter.Count - 1);
                    _writer.WriteLine("]");
                    _writer.Flush();
                }
                _writer = null; 
            }
            void IDisposable.Dispose() { EndArray(); }
        }

        /// <summary>
        /// Used to write an array of messages as the output rather than a single message.
        /// </summary>
        /// <example>
        /// <code>
        /// using(writer.StartArray())
        ///     foreach(IMessageLite m in messages)
        ///         writer.WriteMessage(m);
        /// </code>
        /// </example>
        public JsonArray StartArray()
        {
            if (_isArray) Seperator();
            _isArray = true;
            return new JsonArray(this);
        }
    }
}