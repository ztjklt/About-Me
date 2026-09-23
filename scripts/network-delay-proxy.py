"""TCP delay shim for local WebSocket probes; fixed one-way latency, preserves order."""
import argparse
import asyncio

async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--listen', type=int, required=True)
    parser.add_argument('--target', type=int, required=True)
    parser.add_argument('--delay-ms', type=float, default=100)
    args = parser.parse_args()
    async def handle(reader, writer):
        upstream, upstream_writer = await asyncio.open_connection('127.0.0.1', args.target)
        async def pipe(source, destination):
            queue = asyncio.Queue()
            async def receive():
                try:
                    while data := await source.read(65536):
                        await queue.put((asyncio.get_running_loop().time()+args.delay_ms/1000, data))
                finally:
                    await queue.put((0, None))
            task = asyncio.create_task(receive())
            try:
                while True:
                    due, data = await queue.get()
                    if data is None: break
                    await asyncio.sleep(max(0, due-asyncio.get_running_loop().time()))
                    destination.write(data)
                    await destination.drain()
            finally:
                task.cancel()
                destination.close()
        await asyncio.gather(pipe(reader, upstream_writer), pipe(upstream, writer), return_exceptions=True)
    server = await asyncio.start_server(handle, '127.0.0.1', args.listen)
    async with server: await server.serve_forever()

if __name__ == '__main__': asyncio.run(main())
