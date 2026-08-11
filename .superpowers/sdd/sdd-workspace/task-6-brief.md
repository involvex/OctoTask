# Task 6: Modify `MainWindow.xaml` — Add TabControl with Ports Tab

**Files:**
- Modify: `MainWindow.xaml`

**Interfaces:**
- Consumes: `PortViewerControl` from Task 4 (referenced as `views:PortViewerControl`)
- Consumes: `PortViewModel` from Task 3 (via `{Binding PortVM}`)

## What To Do

Make three changes to `MainWindow.xaml`:

### Change 1: Add the views namespace

In the `<Window>` tag (around line 8), add this namespace after the existing `xmlns:local` declaration:

```xml
         xmlns:views="clr-namespace:OctoTask.UI.Views"
```

### Change 2: Add TabControl and TabItem styles

Add these styles inside `<Window.Resources>` (after the existing `DangerButton` style, around line 296):

```xml
        <!-- TabControl style -->
        <Style TargetType="TabControl">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="0"/>
        </Style>

        <!-- TabItem style -->
        <Style TargetType="TabItem">
            <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
            <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="1,1,1,0"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TabItem">
                        <Border x:Name="Border"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="6,6,0,0"
                                Padding="{TemplateBinding Padding}"
                                Margin="2,0,2,0">
                            <ContentPresenter ContentSource="Header"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Border" Property="Background" Value="{StaticResource SurfaceAltBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="Border" Property="Background" Value="{StaticResource BgBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="Border" Property="BorderThickness" Value="1,2,1,0"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

### Change 3: Wrap main content in TabControl

The current `<DockPanel Grid.Row="2">` (line 422) contains the entire main content. Replace the `<DockPanel Grid.Row="2">` opening tag with a `<TabControl>` that has two tabs:

**Replace** the line:
```xml
        <DockPanel Grid.Row="2">
```

**With:**
```xml
        <!-- Main content: TabControl with Processes and Ports tabs -->
        <TabControl x:Name="MainTabControl" Grid.Row="2"
                    Background="Transparent"
                    BorderThickness="0"
                    Padding="0">
```

**Then wrap** the existing content in the first TabItem. The existing DockPanel content (lines 422-696) should be placed inside a `<TabItem>` with header " Processes ".

**After the existing content** (after the closing `</DockPanel>` that was the main content wrapper, around line 696), **add** the Ports tab:

```xml
            <!-- Ports Tab -->
            <TabItem>
                <TabItem.Header>
                    <TextBlock Text=" Ports " FontSize="12" Padding="4,2"/>
                </TabItem.Header>
                <views:PortViewerControl DataContext="{Binding PortVM}"/>
            </TabItem>
        </TabControl>
```

**Remove** the closing `</DockPanel>` that wrapped the old main content, since it's now inside the TabControl.

The structure should be:
```
<Grid>
  <Grid.RowDefinitions>...</Grid.RowDefinitions>
  
  <!-- Row 0: Dashboard -->
  ...
  
  <!-- Row 1: Toolbar -->
  ...
  
  <!-- Row 2: TabControl -->
  <TabControl x:Name="MainTabControl" Grid.Row="2" ...>
    <TabItem>  <!-- Processes tab -->
      <DockPanel>
        <Border x:Name="DetailsPane" ...>...</Border>
        <Grid>
          <DataGrid .../>
          <TreeView .../>
        </Grid>
      </DockPanel>
    </TabItem>
    <TabItem>  <!-- Ports tab -->
      <views:PortViewerControl DataContext="{Binding PortVM}"/>
    </TabItem>
  </TabControl>
  
  <!-- Row 3: Status bar -->
  ...
</Grid>
```

## Verification

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED
