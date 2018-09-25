# .NET Core gRPC demo

[gRPC](https://grpc.io) is part of the repository (no need to install) under the `tools` directory.

In order to compile `.proto` file, execute one of the scripts under the `scripts/proto` directory, depending on your OS.

In order to start the application, run the server and the client separately:

`dotnet run --project src/GrpcDemo.Server`

`dotnet run --project src/GrpcDemo.Client`

### Available methods:

1. send
2. send with reply
3. send as stream
4. send as stream with reply as stream
5. push
6. pull
7. push many as stream
8. pull many (beware of 4MB message size limit)
9. pull many as stream