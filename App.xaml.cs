using System;
using System.Windows;
using FontResolver.PdfSharp;

namespace PdfTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            FontResolverPdfSharp.Register();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Font Registration Warning: {ex.Message}");
        }
    }
}

