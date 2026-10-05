param([string]$Dll, [string]$Out)
Add-Type -AssemblyName System.Reflection.Metadata
$fs = [IO.File]::OpenRead($Dll)
$pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
$md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)

function TypeName($h) {
  if ($h.IsNil) { return '' }
  switch ($h.Kind) {
    'TypeDefinition' { $t = $md.GetTypeDefinition([System.Reflection.Metadata.TypeDefinitionHandle]$h); return ($md.GetString($t.Namespace) + '.' + $md.GetString($t.Name)).TrimStart('.') }
    'TypeReference'  { $t = $md.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$h); return ($md.GetString($t.Namespace) + '.' + $md.GetString($t.Name)).TrimStart('.') }
    'TypeSpecification' { return '<generic>' }
  }
  return '?'
}

# Minimal signature decoder for field types
function SigType([System.Reflection.Metadata.BlobReader]$r) {
  $b = $r.ReadByte()
  switch ($b) {
    0x02 {'bool'} 0x03 {'char'} 0x04 {'sbyte'} 0x05 {'byte'} 0x06 {'short'} 0x07 {'ushort'}
    0x08 {'int'} 0x09 {'uint'} 0x0A {'long'} 0x0B {'ulong'} 0x0C {'float'} 0x0D {'double'}
    0x0E {'string'} 0x1C {'object'} 0x18 {'IntPtr'}
    0x11 { TypeName ($r.ReadTypeHandle()) }
    0x12 { TypeName ($r.ReadTypeHandle()) }
    0x1D { (SigType $r) + '[]' }
    0x13 { '!' + $r.ReadCompressedInteger() }
    0x1E { '!!' + $r.ReadCompressedInteger() }
    0x0F { (SigType $r) + '*' }
    0x10 { 'ref ' + (SigType $r) }
    0x15 { $g = SigType $r; $n = $r.ReadCompressedInteger(); $a = @(); for ($i=0;$i -lt $n;$i++){ $a += SigType $r }; "$g<" + ($a -join ',') + '>' }
    default { "?0x{0:X}" -f $b }
  }
}

$sb = [Text.StringBuilder]::new()
foreach ($th in $md.TypeDefinitions) {
  $t = $md.GetTypeDefinition($th)
  $name = $md.GetString($t.Name); $ns = $md.GetString($t.Namespace)
  $decl = $t.GetDeclaringType()
  if (-not $decl.IsNil) { $name = (TypeName $decl) + '+' + $name; $ns = '' }
  $kind = if ($t.Attributes -band [Reflection.TypeAttributes]::Interface) {'interface'} else {'class'}
  [void]$sb.AppendLine("$kind $(($ns + '.' + $name).TrimStart('.')) : $(TypeName $t.BaseType)")
  foreach ($fh in $t.GetFields()) {
    $f = $md.GetFieldDefinition($fh)
    $r = $md.GetBlobReader($f.Signature); [void]$r.ReadByte()
    $ft = try { SigType $r } catch { '?' }
    $attrs = @()
    foreach ($ch in $f.GetCustomAttributes()) { $ca = $md.GetCustomAttribute($ch); if ($ca.Constructor.Kind -eq 'MemberReference') { $mr = $md.GetMemberReference($ca.Constructor); $attrs += (TypeName $mr.Parent).Split('.')[-1] } }
    $st = if ($f.Attributes -band [Reflection.FieldAttributes]::Static) {'static '} else {''}
    $vis = if (($f.Attributes -band 7) -eq 6) {'public '} else {''}
    [void]$sb.AppendLine("    $vis$st$ft $($md.GetString($f.Name))" + $(if ($attrs) { '  [' + ($attrs -join ',') + ']' } else { '' }))
  }
  foreach ($mh in $t.GetMethods()) { $m = $md.GetMethodDefinition($mh); [void]$sb.AppendLine("    m " + $md.GetString($m.Name)) }
}
[IO.File]::WriteAllText($Out, $sb.ToString())
