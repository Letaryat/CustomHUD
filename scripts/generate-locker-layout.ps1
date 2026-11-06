<#
.SYNOPSIS
    Generates menu_locker.vxml, the page layout every LockerProfiles profile points at.

.DESCRIPTION
    The locker repeats one slot anatomy for every row of every region, which is
    too much to keep in step by hand: LockerProfiles.Row names each panel and
    variable below, and a slot drawn here without its name there (or the other
    way round) either never fills or shows its {s:...} placeholder.

    Regions and slot counts follow LockerProfiles.Regions and MainSlots. A region
    only carries the parts its designs use (pips in the banner and foot, swatches
    and logos in the list), which keeps the layout near 3 600 panels.

    Rerun after changing a count or the anatomy, then rerun watch-panorama.ps1.
#>
[CmdletBinding()]
param([string] $Output = $null)

$ErrorActionPreference = 'Stop'
if (-not $Output) { $Output = Join-Path $PSScriptRoot '..\resources\panorama\layout\custom_game\customhud\menu_locker.vxml' }
$Output = [IO.Path]::GetFullPath($Output)

$MainSlots = 48
$Pips = 20
$Sidebar = 12
$Tabs = 8
$Chips = 3
$Head = 6
# Name, slot count, parts. Must match LockerProfiles.Regions.
$Regions = @(
    @{ Name = 'banner';    Slots = 5;  Parts = 'icon thumb logo mark glyph rating tags note value meter pips' },
    @{ Name = 'detail';    Slots = 28; Parts = 'icon thumb mark glyph tags note value switch chev meter inline' },
    @{ Name = 'detailbar'; Slots = 3;  Parts = 'glyph value' },
    @{ Name = 'foot';      Slots = 4;  Parts = 'thumb mark glyph tags note value meter pips inline' },
    @{ Name = 'aside';     Slots = 8;  Parts = 'icon mark glyph rating tags note value' },
    @{ Name = 'strip';     Slots = 10; Parts = 'mark glyph note value' }
)
$MainParts = 'icon thumb logo mark swatch glyph rating tags note value switch chev meter meter2 inline'

$sb = New-Object System.Text.StringBuilder
function Line([int] $Depth, [string] $Text) { [void] $sb.Append("`t" * $Depth).AppendLine($Text) }

function Slot([int] $Depth, [string] $Prefix, [int] $Index, [string] $Parts) {
    $id = "$Prefix$Index"
    $has = @{}
    foreach ($part in $Parts.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)) { $has[$part] = $true }
    $n = 'hittest="false"'
    Line $Depth "<Panel id=""$id"" class=""lk-slot hidden"">"
    Line ($Depth + 1) "<Button id=""${id}_hit"" class=""lk-hit"">"
    $d = $Depth + 2
    if ($has.icon) { Line $d "<Panel id=""${id}_icon"" class=""lk-rarity hidden"" $n />" }
    if ($has.thumb) { Line $d "<Panel id=""${id}_thumb"" class=""lk-thumb hidden"" $n />" }
    if ($has.logo) { Line $d "<Panel id=""${id}_logo"" class=""lk-logo hidden"" $n />" }
    if ($has.mark) { Line $d "<Panel class=""lk-mark"" $n><Label class=""lk-mark__text"" text=""{s:${id}_mark}"" $n /></Panel>" }
    # Over the art and under everything else: a view's fade at a picture's edge, or the art itself where there is none.
    if ($has.thumb -or $has.mark) { Line $d "<Panel class=""lk-shade"" $n />" }
    if ($has.swatch) { Line $d "<Panel class=""lk-swatch"" $n />" }
    if ($has.glyph) { Line $d "<Panel id=""${id}_glyph"" class=""chud-ico lk-glyph hidden"" $n><Panel class=""chud-ico__badge"" $n /></Panel>" }
    if ($has.rating) {
        Line $d "<Panel class=""lk-rating"" $n><Label class=""lk-rating__major"" text=""{s:${id}_rating}"" $n /><Label class=""lk-rating__minor"" text=""{s:${id}_rating_minor}"" $n /></Panel>"
    }
    Line $d "<Panel class=""lk-text"" $n>"
    Line ($d + 1) "<Label class=""lk-type"" text=""{s:${id}_type}"" $n />"
    Line ($d + 1) "<Label class=""lk-name"" text=""{s:$id}"" $n />"
    Line ($d + 1) "<Label class=""lk-unit"" text=""{s:${id}_unit}"" $n />"
    Line ($d + 1) "<Label class=""lk-sub"" text=""{s:${id}_sub}"" $n />"
    Line $d "</Panel>"
    if ($has.tags) { Line $d "<Panel class=""lk-tags"" $n><Label class=""lk-tag"" text=""{s:${id}_tag}"" $n /><Label class=""lk-tag2"" text=""{s:${id}_tag2}"" $n /></Panel>" }
    if ($has.note) { Line $d "<Label class=""lk-note"" text=""{s:${id}_note}"" $n />" }
    if ($has.value) {
        # The value's own group: a second glyph ahead of it (LockerProfiles.Row's ValueGlyphPanel),
        # so a currency icon stays beside a right-aligned price of any width.
        $vglyph = if ($has.glyph) { "<Panel id=""${id}_vglyph"" class=""chud-ico lk-vglyph hidden"" $n><Panel class=""chud-ico__badge"" $n /></Panel>" } else { '' }
        Line $d "<Panel class=""lk-end"" $n>$vglyph<Label class=""lk-value"" text=""{s:${id}_value}"" $n /></Panel>"
    }
    if ($has.switch) { Line $d "<Panel class=""lk-switch"" $n><Panel class=""lk-switch__knob"" $n /></Panel>" }
    if ($has.chev) { Line $d "<Label class=""lk-chev"" text=""›"" $n />" }
    if ($has.meter) { Line $d "<Panel class=""chud-meter lk-meter"" $n><Panel class=""chud-meter__fill"" $n /></Panel>" }
    if ($has.meter2) { Line $d "<Panel class=""lk-meter2"" $n><Panel class=""lk-meter2__fill"" $n /></Panel>" }
    if ($has.pips) {
        Line $d "<Panel class=""lk-pips"" $n>"
        for ($p = 0; $p -lt $Pips; $p++) { Line ($d + 1) "<Panel class=""lk-pip lk-pip-$p"" $n />" }
        Line $d "</Panel>"
    }
    Line ($Depth + 1) '</Button>'
    if ($has.inline) {
        Line ($Depth + 1) "<Button id=""${id}_inline"" class=""lk-inline""><Label class=""lk-inline__label"" text=""{s:${id}_inline}"" $n /><Label class=""lk-inline__value"" text=""{s:${id}_inline_value}"" $n /></Button>"
    }
    Line $Depth '</Panel>'
}

function Region([int] $Depth, [hashtable] $Region) {
    for ($i = 0; $i -lt $Region.Slots; $i++) { Slot $Depth "$($Region.Name)_" $i $Region.Parts }
}

$n = 'hittest="false"'
Line 0 '<root>'
Line 1 '<!-- Generated by scripts/generate-locker-layout.ps1. Do not edit. -->'
Line 1 '<styles>'
foreach ($sheet in 'customhud_tokens', 'menu', 'menu_mouse', 'econ_thumbs', 'locker_art', 'customhud_icons',
                   'locker', 'locker_shop', 'locker_maps', 'locker_ranks', 'locker_vip', 'locker_admins', 'locker_mix') {
    Line 2 "<include src=""s2r://panorama/styles/custom_game/customhud/$sheet.vcss_c"" />"
}
Line 1 '</styles>'
Line 1 "<Panel class=""chud-root"" $n>"
Line 2 '<Panel id="shell" class="lk hidden">'

Line 3 "<Panel id=""title"" class=""lk-head"" $n>"
Line 4 "<Panel class=""lk-titles"" $n>"
Line 5 "<Label class=""lk-eyebrow"" text=""{s:eyebrow}"" $n />"
Line 5 "<Panel class=""lk-heading"" $n>"
Line 6 "<Label class=""lk-crumb"" text=""{s:crumb}"" $n />"
Line 6 "<Label class=""lk-crumb-sep"" text=""›"" $n />"
Line 6 "<Label class=""lk-title"" text=""{s:title}"" $n />"
Line 5 '</Panel>'
Line 4 '</Panel>'
Line 4 "<Panel class=""lk-chips"" $n>"
for ($i = 0; $i -lt $Chips; $i++) {
    Line 5 "<Panel id=""chip_$i"" class=""lk-chip hidden"" $n>"
    Line 6 "<Panel id=""chip_${i}_glyph"" class=""chud-ico lk-chip__glyph hidden"" $n><Panel class=""chud-ico__badge"" $n /></Panel>"
    Line 6 "<Panel id=""chip_${i}_thumb"" class=""lk-thumb lk-chip__thumb hidden"" $n />"
    Line 6 "<Panel class=""lk-rating"" $n><Label class=""lk-rating__major"" text=""{s:chip_${i}_rating}"" $n /><Label class=""lk-rating__minor"" text=""{s:chip_${i}_rating_minor}"" $n /></Panel>"
    Line 6 "<Label class=""lk-chip__value"" text=""{s:chip_$i}"" $n />"
    Line 6 "<Label class=""lk-chip__label"" text=""{s:chip_${i}_label}"" $n />"
    Line 5 '</Panel>'
}
Line 4 '</Panel>'
Line 3 '</Panel>'
Line 3 "<Panel class=""lk-divider"" $n />"

Line 3 '<Panel class="lk-content">'
Line 4 '<Panel class="lk-side">'
for ($i = 0; $i -lt $Sidebar; $i++) {
    Line 5 "<Button id=""category_$i"" class=""lk-category hidden""><Panel id=""category_${i}_icon"" class=""chud-ico"" $n><Panel class=""chud-ico__badge"" $n /></Panel><Label class=""lk-category__label"" text=""{s:category}"" $n /><Label class=""lk-category__count"" text=""{s:category_count}"" $n /></Button>"
}
Line 4 '</Panel>'
Line 4 '<Panel class="lk-main">'
Line 5 '<Panel id="tabs" class="lk-tabs hidden">'
for ($i = 0; $i -lt $Tabs; $i++) {
    Line 6 "<Button id=""tab_$i"" class=""lk-tab hidden""><Label class=""lk-tab__label"" text=""{s:tab}"" $n /><Label class=""lk-tab__count"" text=""{s:tab_count}"" $n /></Button>"
}
Line 5 '</Panel>'
Line 5 '<Panel class="lk-strip">'
Region 6 ($Regions | Where-Object Name -eq 'strip')
Line 5 '</Panel>'
Line 5 '<Panel class="lk-body">'
Line 6 '<Panel class="lk-aside">'
Region 7 ($Regions | Where-Object Name -eq 'aside')
Line 6 '</Panel>'
Line 6 '<Panel class="lk-col">'
Line 7 '<Panel class="lk-banner">'
Region 8 ($Regions | Where-Object Name -eq 'banner')
Line 7 '</Panel>'
Line 7 "<Panel id=""thead"" class=""lk-thead"" $n>"
for ($i = 0; $i -lt $Head; $i++) { Line 8 "<Label class=""lk-col-head lk-col-head-$i"" text=""{s:col_$i}"" $n />" }
Line 7 '</Panel>'
# The box takes the column's leftover height, so the scroll panel inside it is a plain 100%
# like the WeaponSkins grid; the items stay out of hit-testing so nothing lies over the scrollbar.
Line 7 "<Panel class=""lk-listbox"" $n>"
Line 8 '<Panel id="options" class="lk-list chud-scroll">'
Line 9 "<Panel class=""lk-items"" $n>"
for ($i = 0; $i -lt $MainSlots; $i++) { Slot 10 'option_' $i $MainParts }
Line 9 '</Panel>'
Line 8 '</Panel>'
Line 7 '</Panel>'
Line 7 '<Panel class="lk-foot">'
Region 8 ($Regions | Where-Object Name -eq 'foot')
Line 7 '</Panel>'
Line 6 '</Panel>'
Line 6 '<Panel class="lk-detail">'
Line 7 "<Panel class=""lk-detail__box"" $n>"
Line 8 '<Panel class="lk-detail__body chud-scroll">'
Line 9 "<Panel class=""lk-detail__items"" $n>"
Region 10 ($Regions | Where-Object Name -eq 'detail')
Line 9 '</Panel>'
Line 8 '</Panel>'
Line 7 '</Panel>'
Line 7 '<Panel class="lk-detail__bar">'
Region 8 ($Regions | Where-Object Name -eq 'detailbar')
Line 7 '</Panel>'
Line 6 '</Panel>'
Line 5 '</Panel>'
Line 4 '</Panel>'
Line 3 '</Panel>'

Line 3 '<Panel class="chud-bar lk-bar">'
Line 4 "<Button id=""nav_back"" class=""chud-nav chud-nav--back hidden""><Label text=""&lt;"" $n /></Button>"
Line 4 "<Label id=""footer"" class=""chud-bar__hint"" text=""{s:footer}"" $n />"
Line 4 "<Label id=""page"" class=""chud-bar__page hidden"" text=""{s:page}"" $n />"
Line 4 "<Button id=""nav_previous"" class=""chud-nav chud-nav--page hidden""><Label text=""‹"" $n /></Button>"
Line 4 "<Button id=""nav_next"" class=""chud-nav chud-nav--page hidden""><Label text=""›"" $n /></Button>"
Line 4 "<Button id=""nav_close"" class=""chud-nav chud-nav--close hidden""><Label text=""×"" $n /></Button>"
Line 3 '</Panel>'
Line 2 '</Panel>'
Line 1 '</Panel>'
Line 0 '</root>'

[IO.File]::WriteAllText($Output, $sb.ToString().Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding $false))
$panels = ([regex]::Matches($sb.ToString(), '<(Panel|Button|Label)\b')).Count
Write-Host ("{0} panels -> {1}" -f $panels, $Output)
