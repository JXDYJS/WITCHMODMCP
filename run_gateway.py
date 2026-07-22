"""Entry point for WitchModMCP MCP gateway. Ensures the package is importable
regardless of how python discovers the current working directory."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mcp_gateway.server import main
main()
