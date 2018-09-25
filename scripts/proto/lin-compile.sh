# output grpc from proto file
PROTOC=tools/Grpc.Tools.1.15.0/linux_x64/protoc
PLUGIN=tools/Grpc.Tools.1.15.0/linux_x64/grpc_csharp_plugin
PROJECT=src/GrpcDemo.Core
PROTO=GrpcDemo.proto

$PROTOC --csharp_out $PROJECT --grpc_out $PROJECT --plugin=protoc-gen-grpc=$PLUGIN $PROJECT/$PROTO