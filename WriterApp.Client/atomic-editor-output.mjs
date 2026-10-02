import { randomUUID } from "node:crypto";
import { mkdir, readFile, readdir, rename, rm, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";

// Replacing the directory entry avoids truncating a file mapped by a Windows host.
// Keep identical assets untouched, including their timestamps.
export async function publishAsset(destination, content, retirementDirectory) {
  const bytes = Buffer.from(content);
  try {
    if ((await readFile(destination)).equals(bytes)) return;
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
  }

  await mkdir(dirname(destination), { recursive: true });
  const temporary = `${destination}.${randomUUID()}.tmp`;
  try {
    await writeFile(temporary, bytes, { flag: "wx" });
    try {
      await rename(temporary, destination);
    } catch (error) {
      if (process.platform !== "win32" || !["EPERM", "EACCES", "EBUSY"].includes(error.code) || !retirementDirectory) throw error;
      // Windows permits renaming a mapped file, but cannot delete it as part
      // of replacement. Retire it outside wwwroot until the mapping closes.
      await mkdir(retirementDirectory, { recursive: true });
      const retired = join(retirementDirectory, `${randomUUID()}.previous`);
      await rename(destination, retired);
      try {
        await rename(temporary, destination);
      } catch (publishError) {
        await rename(retired, destination);
        throw publishError;
      }
    }
  } catch (error) {
    throw new Error(`Could not publish ${destination}. If a running device host holds an exclusive file lock, close it and rebuild.`, { cause: error });
  } finally {
    await rm(temporary, { force: true });
  }
}

/** @returns {import("vite").Plugin} */
export function atomicEditorOutput(destinationDirectory, retirementDirectory) {
  return {
    name: "atomic-editor-output",
    async writeBundle(_options, bundle) {
      await mkdir(retirementDirectory, { recursive: true });
      for (const name of await readdir(retirementDirectory)) {
        if (!/^[a-f0-9-]{36}\.previous$/.test(name)) continue;
        try {
          await rm(join(retirementDirectory, name));
        } catch (error) {
          if (!["EPERM", "EACCES", "EBUSY"].includes(error.code)) throw error;
        }
      }
      for (const output of Object.values(bundle)) {
        await publishAsset(
          join(destinationDirectory, output.fileName),
          output.type === "asset" ? output.source : output.code,
          retirementDirectory
        );
      }
    }
  };
}
