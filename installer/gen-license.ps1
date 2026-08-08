# Generate license.rtf from license.txt (UTF-8 source -> pure ASCII RTF with styled layout)
# Style: title 18pt bold slate + underline rule, version 10pt gray,
#        headings 13pt bold accent, body 11pt dark with 2-char first-line indent,
#        numbered items without indent, compact spacing.

$ErrorActionPreference = 'Stop'

function Esc-Rtf($s) {
    $o = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        $c = [int]$ch
        if ($c -lt 128) {
            if ($c -eq 92 -or $c -eq 123 -or $c -eq 125) {
                [void]$o.Append('\'); [void]$o.Append([char]$c)
            } else {
                [void]$o.Append($ch)
            }
        } else {
            [void]$o.Append("\u$c" + '?')
        }
    }
    return $o.ToString()
}

$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$lines = [System.IO.File]::ReadAllLines(
    (Join-Path $base 'license.txt'),
    [System.Text.Encoding]::UTF8) | Where-Object { $_.Trim().Length -gt 0 }

$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("{\rtf1\ansi\deff0`n")
[void]$sb.Append("{\fonttbl{\f0\fnil\fcharset134 Microsoft YaHei;}}`n")
[void]$sb.Append("{\colortbl;\red0\green0\blue0;\red120\green120\blue120;}`n")
[void]$sb.Append("\f0\sl240\slmult1\sa60\pard`n")

for ($i = 0; $i -lt $lines.Count; $i++) {
    $t = $lines[$i].Trim()
    if ($i -eq 0) {
        # Title: no indent, 15pt bold black, bottom rule
        [void]$sb.Append('\pard\sl240\slmult1\fs30\b\cf1 ' + (Esc-Rtf $t) + '\b0\cf0\sa120\brdrb\brdrs\brdrw10\brsp40\par ')
    } elseif ($i -eq 1) {
        # Version: no indent, 9pt gray
        [void]$sb.Append('\pard\sl240\slmult1\fs18\cf2 ' + (Esc-Rtf $t) + '\cf0\sa80\par ')
    } elseif ($t.Length -le 6) {
        # Heading: 1-char indent, 11pt bold black, space before for grouping
        [void]$sb.Append('\pard\sl240\slmult1\fs22\b\cf1\fi220 ' + (Esc-Rtf $t) + '\b0\cf0\sb100\sa80\par ')
    } elseif ($i -eq $lines.Count - 1) {
        # Closing line: standalone, 1-char indent, 12pt bold black, extra space above
        [void]$sb.Append('\pard\sl240\slmult1\fs24\b\cf1\fi220\sb160 ' + (Esc-Rtf $t) + '\b0\cf0\sa60\par ')
    } elseif ($t -match '^[0-9]\.') {
        # Numbered item: 1-char indent, 10pt black
        [void]$sb.Append('\pard\sl240\slmult1\fs20\cf1\fi220 ' + (Esc-Rtf $t) + '\cf0\sa60\par ')
    } else {
        # Body: 2-char first-line indent, 10pt black
        [void]$sb.Append('\pard\sl240\slmult1\fs20\cf1\fi400 ' + (Esc-Rtf $t) + '\cf0\sa60\par ')
    }
}
[void]$sb.Append("`n}")

[System.IO.File]::WriteAllText(
    (Join-Path $base 'license.rtf'),
    $sb.ToString(),
    [System.Text.Encoding]::ASCII)

$raw = [System.IO.File]::ReadAllText((Join-Path $base 'license.rtf'), [System.Text.Encoding]::ASCII)
Write-Host ("nonAscii={0} fs30={1} fs22={2} cf1={3} cf2={4} indent={5} rule={6}" -f `
    ([regex]::Matches($raw, '[^\x00-\x7F]').Count),
    ([regex]::Matches($raw, '\\fs30').Count),
    ([regex]::Matches($raw, '\\fs22').Count),
    ([regex]::Matches($raw, '\\cf1').Count),
    ([regex]::Matches($raw, '\\cf2').Count),
    ([regex]::Matches($raw, '\\fi4\\d\\d').Count),
    ([regex]::Matches($raw, '\\brdrb').Count))
Write-Host "license.rtf generated."
