﻿using System;
using Assist.Game.Controls.Navigation;
using Assist.Game.Views.Leagues.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Assist.Game.Views.Leagues;

public partial class QueuePage : UserControl
{
    private readonly QueuePageViewModel _viewModel;
    
    public QueuePage()
    {
        DataContext = _viewModel = new QueuePageViewModel();
        InitializeComponent();
    }



    private async void CancelQueue_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        btn.IsEnabled = false;
        
        await _viewModel.LeaveQueue();
        btn.IsEnabled = true;
    }

    private async void QueuePage_Init(object? sender, EventArgs e)
    {
        if (Design.IsDesignMode)
            return;
        
        await _viewModel.Setup();
    }

    private void QueuePage_Loaded(object? sender, RoutedEventArgs e)
    {
         VerticalGameNavigation.Instance.DisableAll();       
    }

    private void QueuePage_Unloaded(object? sender, RoutedEventArgs e)
    {
    }
}