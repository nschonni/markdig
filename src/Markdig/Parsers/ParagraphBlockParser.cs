// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.
using Markdig.Helpers;
using Markdig.Syntax;

namespace Markdig.Parsers
{
    /// <summary>
    /// Block parser for a <see cref="ParagraphBlock"/>.
    /// </summary>
    /// <seealso cref="BlockParser" />
    public class ParagraphBlockParser : BlockParser
    {
        public bool ParseSetexHeadings { get; set; } = true;

        public override BlockState TryOpen(BlockProcessor processor)
        {
            if (processor.IsBlankLine)
            {
                return BlockState.None;
            }

            // We continue trying to match by default
            processor.NewBlocks.Push(new ParagraphBlock(this)
            {
                Column = processor.Column,
                Span = new SourceSpan(processor.Line.Start, processor.Line.End)
            });
            return BlockState.Continue;
        }

        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            if (processor.IsBlankLine)
            {
                return BlockState.BreakDiscard;
            }

            if (ParseSetexHeadings && !processor.IsCodeIndent && !(block.Parent is QuoteBlock))
            {
                return TryParseSetexHeading(processor, block);
            }

            block.UpdateSpanEnd(processor.Line.End);
            return BlockState.Continue;
        }

        public override bool Close(BlockProcessor processor, Block block)
        {
            if (block is ParagraphBlock paragraph)
            {
                TryMatchLinkReferenceDefinition(ref paragraph.Lines, processor);

                // If Paragraph is empty, we can discard it
                if (paragraph.Lines.Count == 0)
                {
                    return false;
                }

                var lineCount = paragraph.Lines.Count;
                for (int i = 0; i < lineCount; i++)
                {
                    paragraph.Lines.Lines[i].Slice.TrimStart();
                }
                if (lineCount > 0)
                {
                    paragraph.Lines.Lines[lineCount - 1].Slice.TrimEnd();
                }
            }

            return true;
        }

        private BlockState TryParseSetexHeading(BlockProcessor state, Block block)
        {
            var paragraph = (ParagraphBlock) block;
            var headingChar = (char)0;
            bool checkForSpaces = false;
            var line = state.Line;
            var c = line.CurrentChar;
            while (c != '\0')
            {
                if (headingChar == 0)
                {
                    if (c == '=' || c == '-')
                    {
                        headingChar = c;
                        continue;
                    }
                    break;
                }

                if (checkForSpaces)
                {
                    if (!c.IsSpaceOrTab())
                    {
                        headingChar = (char)0;
                        break;
                    }
                }
                else if (c != headingChar)
                {
                    if (c.IsSpaceOrTab())
                    {
                        checkForSpaces = true;
                    }
                    else
                    {
                        headingChar = (char)0;
                        break;
                    }
                }
                c = line.NextChar();
            }

            if (headingChar != 0)
            {
                // If we matched a LinkReferenceDefinition before matching the heading, and the remaining 
                // lines are empty, we can early exit and remove the paragraph
                if (!(TryMatchLinkReferenceDefinition(ref paragraph.Lines, state) && paragraph.Lines.Count == 0))
                {
                    // We dicard the paragraph that will be transformed to a heading
                    state.Discard(paragraph);

                    var level = headingChar == '=' ? 1 : 2;

                    var heading = new HeadingBlock(this)
                    {
                        Column = paragraph.Column,
                        Span = new SourceSpan(paragraph.Span.Start, line.Start),
                        Level = level,
                        Lines = paragraph.Lines,
                    };
                    heading.Lines.Trim();

                    // Remove the paragraph as a pending block
                    state.NewBlocks.Push(heading);

                    return BlockState.BreakDiscard;
                }
            }

            block.UpdateSpanEnd(state.Line.End);

            return BlockState.Continue;
        }

        private bool TryMatchLinkReferenceDefinition(ref StringLineGroup lines, BlockProcessor state)
        {
            bool atLeastOneFound = false;

            while (true)
            {
                // If we have found a LinkReferenceDefinition, we can discard the previous paragraph
                var iterator = lines.ToCharIterator();
                if (LinkReferenceDefinition.TryParse(ref iterator, out LinkReferenceDefinition linkReferenceDefinition))
                {
                    state.Document.SetLinkReferenceDefinition(linkReferenceDefinition.Label, linkReferenceDefinition);
                    atLeastOneFound = true;

                    // Correct the locations of each field
                    linkReferenceDefinition.Line = lines.Lines[0].Line;
                    int startPosition = lines.Lines[0].Slice.Start;

                    linkReferenceDefinition.Span        = linkReferenceDefinition.Span      .MoveForward(startPosition);
                    linkReferenceDefinition.LabelSpan   = linkReferenceDefinition.LabelSpan .MoveForward(startPosition);
                    linkReferenceDefinition.UrlSpan     = linkReferenceDefinition.UrlSpan   .MoveForward(startPosition);
                    linkReferenceDefinition.TitleSpan   = linkReferenceDefinition.TitleSpan .MoveForward(startPosition);

                    lines = iterator.Remaining();
                }
                else
                {
                    break;
                }
            }

            return atLeastOneFound;
        }
    }
}