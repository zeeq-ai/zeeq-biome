// Renders the raster logo assets in public/ from the SVG sources:
//   public/logo.svg    -> logo.png (256px)
//   public/favicon.svg -> favicon.ico (16/32/48) and apple-touch-icon.png (180px)
// Run with `pnpm run render:logo` after changing either SVG.
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const publicDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'public')
const read = name => readFileSync(join(publicDir, name))
const write = (name, data) => writeFileSync(join(publicDir, name), data)

// The mark is taller than wide, so fit it inside a square without distorting it.
const transparent = { r: 0, g: 0, b: 0, alpha: 0 }
async function square(svg, size, { padding = 0, background } = {}) {
  const inner = size - padding * 2
  const mark = await sharp(svg, { density: 1200 })
    .resize(inner, inner, { fit: 'contain', background: transparent })
    .png()
    .toBuffer()
  // An opaque background becomes a solid tile with the mark centered on it.
  return sharp({ create: { width: size, height: size, channels: 4, background: background ?? transparent } })
    .composite([{ input: mark, gravity: 'center' }])
    .png()
    .toBuffer()
}

// PNG-in-ICO: a 6-byte header, one 16-byte directory entry per image, then the PNG data.
function ico(images) {
  const header = Buffer.alloc(6)
  header.writeUInt16LE(1, 2)
  header.writeUInt16LE(images.length, 4)
  let offset = header.length + 16 * images.length
  const entries = images.map(({ size, data }) => {
    const entry = Buffer.alloc(16)
    entry[0] = size
    entry[1] = size
    entry.writeUInt16LE(1, 4)
    entry.writeUInt16LE(32, 6)
    entry.writeUInt32LE(data.length, 8)
    entry.writeUInt32LE(offset, 12)
    offset += data.length
    return entry
  })
  return Buffer.concat([header, ...entries, ...images.map(image => image.data)])
}

const logo = read('logo.svg')
const favicon = read('favicon.svg')

write('logo.png', await square(logo, 256))
write('favicon.ico', ico(await Promise.all([16, 32, 48].map(async size => ({ size, data: await square(favicon, size) })))))
// iOS rounds the corners itself and fills transparency with black, so use a white tile with padding.
write('apple-touch-icon.png', await square(favicon, 180, { padding: 30, background: '#ffffff' }))

console.log('Rendered logo.png, favicon.ico and apple-touch-icon.png')
