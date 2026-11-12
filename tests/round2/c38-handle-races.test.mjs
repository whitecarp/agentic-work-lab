import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {spawnSync} from 'node:child_process';

test('held process identities, inaccessible children and surviving descendants', {skip:process.platform!=='win32'}, ()=>{
 const helper=fs.readFileSync(new URL('../../scripts/round2/process-handles.ps1',import.meta.url),'utf8');
 const source=helper.split("Add-Type -TypeDefinition @'")[1].split("\n'@")[0];
 // Exercise the production Hold/Scan/End algorithm against a deterministic
 // process table. Replace every native entry point, including enumeration.
 const imports=source.indexOf(' [DllImport'), row=source.indexOf(' public class Row');
 const snapshot=source.indexOf(' static Dictionary<uint,PE> Snapshot()'), init=source.indexOf(' public void Init(');
 assert.ok(imports>=0 && row>imports && snapshot>row && init>snapshot);
 const fake=fs.readFileSync(new URL('./fixtures/c38-process-table.cs',import.meta.url),'utf8');
 const managed=source.slice(0,imports)+fake+source.slice(row,snapshot)+
   ' static Dictionary<uint,PE> Snapshot(){return new Dictionary<uint,PE>(Table); }\n'+source.slice(init);
 assert.ok(!managed.includes('DllImport'));
 const command="$ErrorActionPreference='Stop'\nAdd-Type -TypeDefinition @'\n"+managed+"\n'@\n[OwnedProcesses]::Exercise()";
 const r=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',command],{encoding:'utf8',windowsHide:true,timeout:15000});
 assert.equal(r.status,0,r.stderr);assert.match(r.stdout,/lineage checks passed/);
});
