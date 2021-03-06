﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Build.TableOfContents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Xunit;

    using Microsoft.DocAsCode.Build.ConceptualDocuments;
    using Microsoft.DocAsCode.Build.Engine;
    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.DataContracts.Common;
    using Microsoft.DocAsCode.Plugins;
    using Microsoft.DocAsCode.Utility;
    using Microsoft.DocAsCode.Tests.Common;

    [Trait("Owner", "lianwei")]
    [Trait("EntityType", "TocDocumentProcessorTest")]
    [Collection("docfx STA")]
    public class TocDocumentProcessorTest : TestBase
    {
        private string _outputFolder;
        private string _inputFolder;
        private string _templateFolder;
        private readonly FileCreator _fileCreator;
        private ApplyTemplateSettings _applyTemplateSettings;

        private const string RawModelFileExtension = ".raw.json";

        public TocDocumentProcessorTest()
        {
            _outputFolder = GetRandomFolder();
            _inputFolder = GetRandomFolder();
            _templateFolder = GetRandomFolder();
            _applyTemplateSettings = new ApplyTemplateSettings(_inputFolder, _outputFolder);
            _applyTemplateSettings.RawModelExportSettings.Export = true;
            _fileCreator = new FileCreator(_inputFolder);
        }

        [Fact]
        public void ProcessMarkdownTocWithAbsoluteHrefShouldSucceed()
        {
            var content = @"
#[Topic1](/href1)
##Topic1.1
###[Topic1.1.1](/href1.1.1)
##[Topic1.2]()
#[Topic2](http://href.com)
";
            var toc = _fileCreator.CreateFile(content, FileType.MarkdownToc);
            FileCollection files = new FileCollection(_inputFolder);
            files.Add(DocumentType.Article, new[] { toc });
            BuildDocument(files);

            var outputRawModelPath = Path.Combine(_outputFolder, Path.ChangeExtension(toc, RawModelFileExtension));
            Assert.True(File.Exists(outputRawModelPath));
            var model = JsonUtility.Deserialize<TocItemViewModel>(outputRawModelPath);
            var expectedModel = new TocItemViewModel
            {
                Items = new TocViewModel
                {
                    new TocItemViewModel
                    {
                        Name = "Topic1",
                        Href = "/href1",
                        Items = new TocViewModel
                        {
                            new TocItemViewModel
                            {
                                Name = "Topic1.1",
                                Items = new TocViewModel
                                {
                                    new TocItemViewModel
                                    {
                                        Name = "Topic1.1.1",
                                        Href = "/href1.1.1"
                                    }
                                }
                            },
                            new TocItemViewModel
                            {
                                Name = "Topic1.2",
                                Href = string.Empty
                            }
                        }
                    },
                    new TocItemViewModel
                    {
                        Name = "Topic2",
                        Href = "http://href.com"
                    }
                }
            };

            AssertTocEqual(expectedModel, model);
        }

        [Fact]
        public void ProcessMarkdownTocWithRelativeHrefShouldSucceed()
        {
            var file1 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent);
            var file2 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent, "a");
            var content = $@"
#[Topic1](/href1)
##[Topic1.1]({file1})
###[Topic1.1.1]({file2})
##[Topic1.2]()
#[Topic2](http://href.com)
";
            var toc = _fileCreator.CreateFile(content, FileType.MarkdownToc);
            FileCollection files = new FileCollection(_inputFolder);
            files.Add(DocumentType.Article, new[] { file1, file2, toc });
            BuildDocument(files);
            var outputRawModelPath = Path.Combine(_outputFolder, Path.ChangeExtension(toc, RawModelFileExtension));
            Assert.True(File.Exists(outputRawModelPath));
            var model = JsonUtility.Deserialize<TocItemViewModel>(outputRawModelPath);
            var expectedModel = new TocItemViewModel
            {
                Homepage = file1,
                Items = new TocViewModel
                {
                    new TocItemViewModel
                    {
                        Homepage = file1,
                        Name = "Topic1",
                        Href = "/href1",
                        Items = new TocViewModel
                        {
                            new TocItemViewModel
                            {
                                Homepage = file2,
                                Name = "Topic1.1",
                                Href = file1,
                                Items = new TocViewModel
                                {
                                    new TocItemViewModel
                                    {
                                        Name = "Topic1.1.1",
                                        Href = file2
                                    }
                                }
                            },
                            new TocItemViewModel
                            {
                                Name = "Topic1.2",
                                Href = string.Empty
                            }
                        }
                    },
                    new TocItemViewModel
                    {
                        Name = "Topic2",
                        Href = "http://href.com"
                    }
                }
            };

            AssertTocEqual(expectedModel, model);
        }

        [Fact]
        public void ProcessYamlTocWithFolderShouldSucceed()
        {
            var file1 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent);
            var file2 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent, "sub");
            var subToc = _fileCreator.CreateFile($@"
#[Topic]({Path.GetFileName(file2)})
", FileType.MarkdownToc, "sub");
            var content = $@"
- name: Topic1
  href: {file1}
  items:
    - name: Topic1.1
      href: {file1}
      homepage: {file2}
    - name: Topic1.2
      href: sub/
      homepage: {file1}
- name: Topic2
  href: sub/
";
            var toc = _fileCreator.CreateFile(content, FileType.YamlToc);
            FileCollection files = new FileCollection(_inputFolder);
            files.Add(DocumentType.Article, new[] { file1, file2, toc, subToc });
            BuildDocument(files);
            var outputRawModelPath = Path.Combine(_outputFolder, Path.ChangeExtension(toc, RawModelFileExtension));
            Assert.True(File.Exists(outputRawModelPath));
            var model = JsonUtility.Deserialize<TocItemViewModel>(outputRawModelPath);
            var expectedModel = new TocItemViewModel
            {
                Homepage = file1,
                Items = new TocViewModel
                {
                    new TocItemViewModel
                    {
                        Homepage = file1,
                        Name = "Topic1",
                        Href = file1,
                        Items = new TocViewModel
                        {
                            new TocItemViewModel
                            {
                                Name = "Topic1.1",
                                Href = file1, // For relative file, href keeps unchanged
                                Homepage = file2, // Homepage always keeps unchanged
                            },
                            new TocItemViewModel
                            {
                                Name = "Topic1.2",
                                Href = file1, // For relative folder, href should be overwritten by homepage
                                Homepage = file1,
                                TocHref = "sub/toc.md",
                            }
                        }
                    },
                    new TocItemViewModel
                    {
                        Name = "Topic2",
                        Href = file2,
                        TocHref = "sub/toc.md",
                    }
                }
            };

            AssertTocEqual(expectedModel, model);
        }

        [Fact]
        public void ProcessYamlTocWithReferencedTocShouldSucceed()
        {
            var file1 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent);
            var file2 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent, "sub1");
            var file3 = _fileCreator.CreateFile(string.Empty, FileType.MarkdownContent, "sub1/sub2");
            var referencedToc = _fileCreator.CreateFile($@"
- name: Topic
  href: {Path.GetFileName(file3)}
", FileType.YamlToc, "sub1/sub2");
            var subToc = _fileCreator.CreateFile($@"
#[Topic]({Path.GetFileName(file2)})
#[ReferencedToc](sub2/{Path.GetFileName(referencedToc)})
", FileType.MarkdownToc, "sub1");
            var content = $@"
- name: Topic1
  href: {file1}
  items:
    - name: Topic1.1
      href: {subToc}
      items:
        - name: Topic1.1.1
        - name: Topic1.1.2
    - name: Topic1.2
      href: {subToc}
      homepage: {file1}
- name: Topic2
  href: {referencedToc}
";
            var toc = _fileCreator.CreateFile(content, FileType.YamlToc);
            FileCollection files = new FileCollection(_inputFolder);
            files.Add(DocumentType.Article, new[] { file1, file2, file3, toc, subToc });
            BuildDocument(files);
            var outputRawModelPath = Path.Combine(_outputFolder, Path.ChangeExtension(toc, RawModelFileExtension));

            Assert.True(File.Exists(outputRawModelPath));
            var model = JsonUtility.Deserialize<TocItemViewModel>(outputRawModelPath);
            var expectedModel = new TocItemViewModel
            {
                Homepage = file1,
                Items = new TocViewModel
                {
                    new TocItemViewModel
                    {
                        Homepage = file2,
                        Name = "Topic1",
                        Href = file1,
                        Items = new TocViewModel
                        {
                            new TocItemViewModel
                            {
                                Name = "Topic1.1",
                                Href = null, // For referenced toc, the content from the referenced toc is expanded as the items of current toc, and href is cleared
                                Items = new TocViewModel
                                {
                                    new TocItemViewModel
                                    {
                                        Name = "Topic",
                                        Href = file2,
                                    },
                                    new TocItemViewModel
                                    {
                                        Name = "ReferencedToc",
                                        Items = new TocViewModel
                                        {
                                            new TocItemViewModel
                                            {
                                                Name = "Topic",
                                                Href = file3,
                                            }
                                        }
                                    }
                                }
                            },
                            new TocItemViewModel
                            {
                                Name = "Topic1.2",
                                Href = file1, // For referenced toc, href should be overwritten by homepage
                                Homepage = file1,
                                Items = new TocViewModel
                                {
                                    new TocItemViewModel
                                    {
                                        Name = "Topic",
                                        Href = file2,
                                    },
                                    new TocItemViewModel
                                    {
                                        Name = "ReferencedToc",
                                        Items = new TocViewModel
                                        {
                                            new TocItemViewModel
                                            {
                                                Name = "Topic",
                                                Href = file3,
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new TocItemViewModel
                    {
                        Name = "Topic2",
                        Href = null,
                        Items = new TocViewModel
                        {
                            new TocItemViewModel
                            {
                                Name = "Topic",
                                Href = file3,
                            }
                        }
                    }
                }
            };

            AssertTocEqual(expectedModel, model);

            // Referenced TOC File should not exist 
            var referencedTocPath = Path.Combine(_outputFolder, Path.ChangeExtension(subToc, RawModelFileExtension));
            Assert.False(File.Exists(referencedTocPath));
        }

        [Fact]
        public void ProcessTocWithCircularReferenceShouldFail()
        {
            var referencedToc = _fileCreator.CreateFile($@"
- name: Topic
  href: TOC.md
", FileType.YamlToc, "sub1");
            var subToc = _fileCreator.CreateFile($@"
#Topic
##[ReferencedToc](Toc.yml)
", FileType.MarkdownToc, "sub1");
            var content = $@"
- name: Topic1
  href: {subToc}
";
            var toc = _fileCreator.CreateFile(content, FileType.YamlToc);
            FileCollection files = new FileCollection(_inputFolder);
            files.Add(DocumentType.Article, new[] { toc, subToc });
            var e = Assert.Throws<DocumentException>(() => BuildDocument(files));
            Assert.Equal($"Circular reference to {Path.GetFullPath(Path.Combine(_inputFolder, subToc)).ToDisplayPath()} is found in {Path.GetFullPath(Path.Combine(_inputFolder, referencedToc)).ToDisplayPath()}", e.Message, true);
        }

        #region Helper methods

        private enum FileType
        {
            MarkdownToc,
            YamlToc,
            MarkdownContent
        }

        private sealed class FileCreator
        {
            private const string MarkdownTocName = "toc.md";
            private const string YamlTocName = "toc.yml";
            private readonly string _rootDir;
            public FileCreator(string rootDir)
            {
                _rootDir = rootDir ?? Environment.CurrentDirectory;
            }

            public string CreateFile(string content, FileType type, string folder = null)
            {
                string fileName;
                switch (type)
                {
                    case FileType.MarkdownToc:
                        fileName = MarkdownTocName;
                        break;
                    case FileType.YamlToc:
                        fileName = YamlTocName;
                        break;
                    case FileType.MarkdownContent:
                        fileName = Path.GetRandomFileName() + ".md";
                        break;
                    default:
                        throw new NotSupportedException(type.ToString());
                }

                fileName = Path.Combine(folder ?? string.Empty, fileName);

                var filePath = Path.Combine(_rootDir, fileName);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, content);
                return fileName.Replace('\\', '/');
            }
        }

        private void BuildDocument(FileCollection files)
        {
            var parameters = new DocumentBuildParameters
            {
                Files = files,
                OutputBaseDir = _outputFolder,
                ApplyTemplateSettings = _applyTemplateSettings,
            };

            using (var builder = new DocumentBuilder(LoadAssemblies()))
            {
                builder.Build(parameters);
            }
        }

        private IEnumerable<Assembly> LoadAssemblies()
        {
            yield return typeof(ConceptualDocumentProcessor).Assembly;
            yield return typeof(TocDocumentProcessor).Assembly;
        }

        private static void AssertTocEqual(TocItemViewModel expected, TocItemViewModel actual)
        {
            using (var swForExpected = new StringWriter())
            {
                YamlUtility.Serialize(swForExpected, expected);
                using (var swForActual = new StringWriter())
                {
                    YamlUtility.Serialize(swForActual, actual);
                    Assert.Equal(swForExpected.ToString(), swForActual.ToString());
                }
            }
        }

        #endregion
    }
}
