using System;
using System.IO;
using System.Xml;
using System.Text;

namespace Google.ProtocolBuffers.Serialization.Http
{
    /// <summary>
    /// Extensions and helpers to abstract the reading/writing of messages by a client-specified content type.
    /// </summary>
    public static class MessageFormatFactory
    {
        /// <summary>
        /// Constructs an ICodedInputStream from the input stream based on the contentType provided
        /// </summary>
        /// <param name="options">Options specific to reading this message and/or content type</param>
        /// <param name="contentType">The mime type of the input stream content</param>
        /// <param name="input">The stream to read the message from</param>
        /// <returns>The ICodedInputStream that can be given to the IBuilder.MergeFrom(...) method</returns>
        public static ICodedInputStream CreateInputStream(MessageFormatOptions options, string contentType, Stream input)
        {
            ICodedInputStream codedInput = ContentTypeToInputStream(contentType, options, input);

            if (codedInput is XmlFormatReader)
            {
                XmlFormatReader reader = (XmlFormatReader)codedInput;
                reader.RootElementName = options.XmlReaderRootElementName;
                reader.Options = options.XmlReaderOptions;
            }

            return codedInput;
        }

        /// <summary>
        /// Merges the message from the input stream based on the contentType provided
        /// </summary>
        /// <typeparam name="TBuilder">A type derived from IBuilderLite</typeparam>
        /// <param name="builder">An instance of a message builder</param>
        /// <param name="options">Options specific to reading this message and/or content type</param>
        /// <param name="contentType">The mime type of the input stream content</param>
        /// <param name="input">The stream to read the message from</param>
        /// <returns>The same builder instance that was supplied in the builder parameter</returns>
        public static TBuilder MergeFrom<TBuilder>(this TBuilder builder, MessageFormatOptions options, string contentType, Stream input) where TBuilder : IBuilderLite
        {
            ICodedInputStream codedInput = CreateInputStream(options, contentType, input);
            codedInput.ReadMessageStart();
            builder.WeakMergeFrom(codedInput, options.ExtensionRegistry);
            codedInput.ReadMessageEnd();
            return builder;
        }
        
        /// <summary>
        /// Writes the message instance to the stream using the content type provided
        /// </summary>
        /// <param name="options">Options specific to writing this message and/or content type</param>
        /// <param name="contentType">The mime type of the content to be written</param>
        /// <param name="output">The stream to write the message to</param>
        /// <remarks> If you do not dispose of ICodedOutputStream some formats may yield incomplete output </remarks>
        public static ICodedOutputStream CreateOutputStream(MessageFormatOptions options, string contentType, Stream output)
        {
            ICodedOutputStream codedOutput = ContentTypeToOutputStream(contentType, options, output);

            if (codedOutput is JsonFormatWriter)
            {
                JsonFormatWriter writer = (JsonFormatWriter)codedOutput;
                if (options.FormattedOutput)
                {
                    writer.Formatted();
                }
            }
            else if (codedOutput is XmlFormatWriter)
            {
                XmlFormatWriter writer = (XmlFormatWriter)codedOutput;
                if (options.FormattedOutput)
                {
                    XmlWriterSettings settings = new XmlWriterSettings()
                                                     {
                                                         CheckCharacters = false,
                                                         NewLineHandling = NewLineHandling.Entitize,
                                                         OmitXmlDeclaration = true,
                                                         Encoding = new UTF8Encoding(false),
                                                         Indent = true,
                                                         IndentChars = "    ",
                                                         NewLineChars = Environment.NewLine,
                                                     };
                    // Don't know how else to change xml writer options?
                    codedOutput = writer = XmlFormatWriter.CreateInstance(XmlWriter.Create(output, settings));
                }
                writer.RootElementName = options.XmlWriterRootElementName;
                writer.Options = options.XmlWriterOptions;
            }

            return codedOutput;
        }

        /// <summary>
        /// Writes the message instance to the stream using the content type provided
        /// </summary>
        /// <param name="message">An instance of a message</param>
        /// <param name="options">Options specific to writing this message and/or content type</param>
        /// <param name="contentType">The mime type of the content to be written</param>
        /// <param name="output">The stream to write the message to</param>
        public static void WriteTo(this IMessageLite message, MessageFormatOptions options, string contentType, Stream output)
        {
            ICodedOutputStream codedOutput = CreateOutputStream(options, contentType, output);

            // Output the appropriate message preamble
            codedOutput.WriteMessageStart();

            // Write the message content to the output
            message.WriteTo(codedOutput);

            // Write the closing message fragment
            codedOutput.WriteMessageEnd();
            codedOutput.Flush();
        }

        private static ICodedInputStream ContentTypeToInputStream(string contentType, MessageFormatOptions options, Stream input)
        {
            contentType = (contentType ?? String.Empty).Split(';')[0].Trim();

            Converter<Stream, ICodedInputStream> factory;
            if(!options.MimeInputTypesReadOnly.TryGetValue(contentType, out factory) || factory == null)
            {
                if(String.IsNullOrEmpty(options.DefaultContentType) ||
                    !options.MimeInputTypesReadOnly.TryGetValue(options.DefaultContentType, out factory) || factory == null)
                {
                    throw new ArgumentOutOfRangeException("contentType");
                }
            }

            return factory(input);
        }

        private static ICodedOutputStream ContentTypeToOutputStream(string contentType, MessageFormatOptions options, Stream output)
        {
            contentType = (contentType ?? String.Empty).Split(';')[0].Trim();

            Converter<Stream, ICodedOutputStream> factory;
            if (!options.MimeOutputTypesReadOnly.TryGetValue(contentType, out factory) || factory == null)
            {
                if (String.IsNullOrEmpty(options.DefaultContentType) ||
                    !options.MimeOutputTypesReadOnly.TryGetValue(options.DefaultContentType, out factory) || factory == null)
                {
                    throw new ArgumentOutOfRangeException("contentType");
                }
            }

            return factory(output);
        }

    }
}