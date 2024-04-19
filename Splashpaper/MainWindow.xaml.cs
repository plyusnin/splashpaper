﻿using Splashpaper.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Splashpaper;

/// <summary>Interaction logic for MainWindow.xaml</summary>
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();

		DataContext = new MainViewModel();
	}

	private void TrayIcon_OnClick(object? Sender, EventArgs e)
	{
		WindowState = WindowState.Normal;
		Activate();
	}

	private void CloseCommand_OnExecuted(object Sender, ExecutedRoutedEventArgs E)
	{
		Close();
	}
}