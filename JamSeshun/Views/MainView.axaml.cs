﻿using Avalonia.Controls;
using JamSeshun.ViewModels;

namespace JamSeshun.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public MainView(TunerViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }
}
