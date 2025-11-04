using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PrintingTools.Core;

namespace PrintingTools.Tests.Validation;

internal sealed class ValidationScenario
{
    public ValidationScenario(string name, Func<ValidationScenarioContext, ValidationScenarioResult> execute)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public string Name { get; }

    public Func<ValidationScenarioContext, ValidationScenarioResult> Execute { get; }
}

internal sealed class ValidationScenarioContext
{
    public ValidationScenarioContext()
    {
        TargetDpi = new Vector(144, 144);
    }

    public Vector TargetDpi { get; init; }
}

internal sealed class ValidationScenarioResult
{
    public ValidationScenarioResult(PrintSession session, string description)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public PrintSession Session { get; }

    public string Description { get; }
}

internal static class ValidationScenarios
{
    private static readonly ValidationScenario[] Scenarios =
    [
        new ValidationScenario("StandardTicket", context =>
        {
            var panel = new StackPanel
            {
                Width = 612,
                Height = 792,
                Spacing = 24,
                Background = Brushes.White,
                Children =
                {
                    CreateHeader("PrintingTools – Standard Ticket"),
                    CreateSeparator(),
                    CreateRectangleRow(Colors.SteelBlue, Colors.LightSteelBlue),
                    CreateRectangleRow(Colors.DarkGoldenrod, Colors.Goldenrod),
                    CreateFooter("Generated: " + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                }
            };

            var document = PrintDocument.FromVisual(panel, new PrintPageSettings
            {
                TargetSize = new Size(612, 792),
                Margins = new Thickness(48)
            });

            var session = new PrintSession(document, new PrintOptions
            {
                PaperSize = new Size(8.5, 11),
                Margins = new Thickness(0.5),
                UsePrintableArea = true,
                LayoutKind = PrintLayoutKind.Standard
            }, description: "Validation Scenario – Standard");

            return new ValidationScenarioResult(session, "Standard letter layout with stacked rectangles.");
        }),
        new ValidationScenario("BookletPreview", context =>
        {
            var grid = new Grid
            {
                Width = 768,
                Height = 512,
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("*,*"),
                Background = Brushes.White
            };

            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    var cell = new Border
                    {
                        Margin = new Thickness(12),
                        BorderBrush = Brushes.DimGray,
                        BorderThickness = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromArgb(255, (byte)(180 + row * 20), (byte)(180 + column * 20), 220)),
                        Child = new Rectangle
                        {
                            Fill = Brushes.WhiteSmoke,
                            Stroke = Brushes.DimGray,
                            StrokeThickness = 1
                        }
                    };

                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, column);
                    grid.Children.Add(cell);
                }
            }

            var document = PrintDocument.FromVisual(grid, new PrintPageSettings
            {
                TargetSize = new Size(612, 792),
                Margins = new Thickness(36)
            });

            var options = new PrintOptions
            {
                PaperSize = new Size(8.5, 11),
                Margins = new Thickness(0.5),
                LayoutKind = PrintLayoutKind.Booklet,
                BookletBindLongEdge = false,
                UsePrintableArea = true
            };

            var session = new PrintSession(document, options, description: "Validation Scenario – Booklet");

            return new ValidationScenarioResult(session, "Booklet layout across four quadrants.");
        }),
        new ValidationScenario("PosterLayout", context =>
        {
            var canvas = new Canvas
            {
                Width = 900,
                Height = 600,
                Background = Brushes.White
            };

            for (var i = 0; i < 6; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = 120,
                    Height = 120,
                    StrokeThickness = 3,
                    Stroke = new SolidColorBrush(Color.FromArgb(255, (byte)(60 + i * 20), (byte)(100 + i * 15), (byte)(180 + i * 10))),
                    Fill = new SolidColorBrush(Color.FromArgb(80, (byte)(200 - i * 20), (byte)(220 - i * 15), (byte)(255 - i * 10)))
                };

                Canvas.SetLeft(ellipse, 60 + i * 120);
                Canvas.SetTop(ellipse, 80 + (i % 2) * 160);
                canvas.Children.Add(ellipse);
            }

            var document = PrintDocument.FromVisual(canvas, new PrintPageSettings
            {
                TargetSize = new Size(842, 1191), // A3 in DIPs (297x420 mm at 96 DPI)
                Margins = new Thickness(24)
            });

            var options = new PrintOptions
            {
                PaperSize = new Size(11.69, 16.54), // A3 in inches
                Margins = new Thickness(0.3),
                LayoutKind = PrintLayoutKind.Poster,
                PosterTileCount = 4,
                NUpRows = 2,
                NUpColumns = 2,
                UsePrintableArea = true
            };

            var session = new PrintSession(document, options, description: "Validation Scenario – Poster");
            return new ValidationScenarioResult(session, "Poster layout with tiled ellipses.");
        })
    ];

    public static IReadOnlyList<ValidationScenario> All => Scenarios;

    private static Control CreateHeader(string text) =>
        new Border
        {
            Padding = new Thickness(16),
            Background = Brushes.SteelBlue,
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                FontSize = 24,
                Foreground = Brushes.White,
                Text = text
            }
        };

    private static Control CreateFooter(string text) =>
        new Border
        {
            Padding = new Thickness(16),
            Background = Brushes.LightGray,
            CornerRadius = new CornerRadius(6),
            Child = new TextBlock
            {
                FontSize = 14,
                Foreground = Brushes.DimGray,
                Text = text
            }
        };

    private static Control CreateSeparator() =>
        new Border
        {
            Height = 2,
            Background = Brushes.LightGray
        };

    private static Control CreateRectangleRow(Color primary, Color secondary) =>
        new UniformGrid
        {
            Rows = 1,
            Columns = 3,
            Margin = new Thickness(0, 8),
            Children =
            {
                CreateCard("Section A", primary, secondary),
                CreateCard("Section B", secondary, primary),
                CreateCard("Section C", primary, secondary)
            }
        };

    private static Control CreateCard(string title, Color header, Color body) =>
        new Border
        {
            Margin = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(header),
            BorderThickness = new Thickness(2),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new Border
                    {
                        Background = new SolidColorBrush(header),
                        Padding = new Thickness(8),
                        Child = new TextBlock
                        {
                            Foreground = Brushes.White,
                            FontSize = 16,
                            Text = title
                        }
                    },
                    new Border
                    {
                        Background = new SolidColorBrush(body),
                        Padding = new Thickness(10),
                        Child = new Rectangle
                        {
                            Fill = Brushes.WhiteSmoke,
                            Stroke = Brushes.LightGray,
                            StrokeThickness = 1,
                            Height = 80,
                            Width = double.NaN
                        }
                    }
                }
            }
        };
}
